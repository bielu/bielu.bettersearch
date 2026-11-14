using System.Net;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Bielu.BetterSearch.Typesense.Configuration;
using Bielu.BetterSearch.Typesense.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Bielu.BetterSearch.Typesense.Tests;

/// <summary>
/// Requests the provider sends, and how it reads the answers, against a fake server: cases a real Typesense can't be
/// made to produce, such as a lost answer, a node that times out, or a count past <see cref="int.MaxValue"/>.
/// </summary>
public sealed class TypesenseFakeServerTests
{
    private static readonly Uri FirstNode = new("http://node-1:8108/");
    private static readonly Uri SecondNode = new("http://node-2:8108/");

    [Theory]
    [InlineData("collections/people/documents/..")]
    [InlineData("collections/people/documents/%2E%2E")]
    [InlineData("collections/people/documents/.")]
    public void Resolve_DotSegment_Throws(string path)
    {
        var error = Should.Throw<ArgumentException>(() => TypesenseHttpClientWrapper.Resolve(FirstNode, path));

        error.Message.ShouldContain("holds a '.' or '..' segment");
    }

    [Fact]
    public async Task RemoveDocumentAsync_DotDotId_DeletesByFilterInsideTheCollection()
    {
        var server = new FakeServer(_ => (HttpStatusCode.OK, """{"num_deleted":1}"""));

        var result = await Indexing(server).RemoveDocumentAsync("..", "people", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var request = server.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Delete);
        request.Uri.AbsolutePath.ShouldBe("/collections/people/documents");
        Uri.UnescapeDataString(request.Uri.Query).ShouldBe("?filter_by=id:[`..`]");
    }

    [Fact]
    public async Task SendAsync_DeleteTimesOut_DoesNotReplayOnTheNextNode()
    {
        var server = new FakeServer(request => request.RequestUri!.Host == FirstNode.Host
            ? throw new TaskCanceledException("timed out")
            : (HttpStatusCode.OK, """{"num_deleted":0}"""));

        await Should.ThrowAsync<TaskCanceledException>(() =>
            Client(server).SendAsync(HttpMethod.Delete, "collections/people/documents?truncate=true", null, TestContext.Current.CancellationToken));

        server.Requests.ShouldHaveSingleItem().Uri.Host.ShouldBe(FirstNode.Host);
    }

    [Fact]
    public async Task SendAsync_DeleteGets5xx_ReturnsItWithoutReplaying()
    {
        var server = new FakeServer(request => request.RequestUri!.Host == FirstNode.Host
            ? (HttpStatusCode.ServiceUnavailable, """{"message":"Not Ready"}""")
            : (HttpStatusCode.OK, """{"num_deleted":0}"""));

        var response = await Client(server).SendAsync(HttpMethod.Delete, "collections/people/documents?truncate=true", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        server.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_DeleteCannotConnect_UsesTheNextNode()
    {
        var server = new FakeServer(request => request.RequestUri!.Host == FirstNode.Host
            ? throw new HttpRequestException(HttpRequestError.ConnectionError, "refused")
            : (HttpStatusCode.OK, """{"num_deleted":2}"""));

        var response = await Client(server).SendAsync(HttpMethod.Delete, "collections/people/documents?truncate=true", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        server.Requests.Select(request => request.Uri.Host).ShouldBe([FirstNode.Host, SecondNode.Host]);
    }

    [Fact]
    public async Task SendAsync_GetGets5xx_UsesTheNextNode()
    {
        var server = new FakeServer(request => request.RequestUri!.Host == FirstNode.Host
            ? (HttpStatusCode.ServiceUnavailable, """{"message":"Not Ready"}""")
            : (HttpStatusCode.OK, "[]"));

        var response = await Client(server).SendAsync(HttpMethod.Get, "collections", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        server.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RemoveAllDocumentsAsync_CountAboveInt32_ReturnsTheWholeCount()
    {
        var server = new FakeServer(_ => (HttpStatusCode.OK, """{"num_deleted":3000000000}"""));

        var result = await Indexing(server).RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = "people" }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        result.Value.ShouldBe(3_000_000_000L);
    }

    [Fact]
    public void RemoveBatches_LongIds_KeepEachFilterWithinTheLimit()
    {
        var ids = Enumerable.Range(0, 30).Select(i => $"https://example.com/{i}/{new string('a', 300)}").ToList();

        var batches = TypesenseIndexingProviderAsync.RemoveBatches(ids);

        batches.Count.ShouldBeGreaterThan(1);
        batches.SelectMany(batch => batch).ShouldBe(ids);
        foreach (var batch in batches)
        {
            Uri.EscapeDataString($"id:[{string.Join(',', batch.Select(id => $"`{id}`"))}]").Length
                .ShouldBeLessThanOrEqualTo(TypesenseIndexingProviderAsync.RemoveBatchFilterLength);
        }
    }

    [Fact]
    public void RemoveBatches_ShortIds_AtMostRemoveBatchSizePerBatch()
    {
        var ids = Enumerable.Range(0, 250).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();

        var batches = TypesenseIndexingProviderAsync.RemoveBatches(ids);

        batches.Select(batch => batch.Count).ShouldBe([100, 100, 50]);
    }

    [Fact]
    public void RemoveBatches_IdLongerThanTheLimit_IsABatchOfItsOwn()
    {
        var huge = new string('a', TypesenseIndexingProviderAsync.RemoveBatchFilterLength + 1);

        var batches = TypesenseIndexingProviderAsync.RemoveBatches(["a", huge, "b"]);

        batches.ShouldBe([["a"], [huge], ["b"]]);
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_MoreThanOneBatch_SendsSeveralImportsAndCountsWhatWasStored()
    {
        var imports = 0;
        var server = new FakeServer(request =>
        {
            var lines = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult().Split('\n').Length;
            // The second import fails as a whole.
            return ++imports == 1
                ? (HttpStatusCode.OK, string.Join('\n', Enumerable.Repeat("""{"success":true}""", lines)))
                : (HttpStatusCode.BadRequest, """{"message":"bad batch"}""");
        });
        var documents = Enumerable.Range(0, TypesenseIndexingProviderAsync.ImportBatchSize + 5)
            .Select(i => new SearchDocument
            {
                Id = $"doc-{i}",
                Type = "Person",
                Index = "people",
                Fields = new Dictionary<string, IList<object>> { ["Name"] = ["Jane"] }
            }).ToList();

        var result = await Indexing(server).IndexMultipleDocumentsAsync(documents, TestContext.Current.CancellationToken);

        server.Requests.Count.ShouldBe(2);
        var error = result.Errors.ShouldHaveSingleItem();
        error.Message.ShouldContain($"{TypesenseIndexingProviderAsync.ImportBatchSize} of {documents.Count} documents were indexed");
        error.Message.ShouldContain("bad batch");
        ((List<string>)error.Metadata["ids"]).ShouldBe(documents.Skip(TypesenseIndexingProviderAsync.ImportBatchSize).Select(document => document.Id));
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_NaN_ReturnsFailedResultWithoutSending()
    {
        var server = new FakeServer(_ => (HttpStatusCode.OK, """{"success":true}"""));
        var document = new SearchDocument
        {
            Id = "nan",
            Type = "Item",
            Index = "items",
            Fields = new Dictionary<string, IList<object>> { ["Price"] = [double.NaN] }
        };

        var result = await Indexing(server).IndexMultipleDocumentsAsync([document], TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("not a finite number Typesense can store");
        server.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task FieldsAsync_RefreshJustAfterARead_UsesTheFieldsJustRead()
    {
        var server = new FakeServer(_ => (HttpStatusCode.OK, """{"fields":[{"name":"i_Age"}]}"""));
        var client = Client(server);
        var schema = new TypesenseSchema(new TestOptionsMonitor<TypesenseOptions>(new TypesenseOptions()));

        await schema.FieldsAsync(client, "people", refresh: false, TestContext.Current.CancellationToken);
        var fields = await schema.FieldsAsync(client, "people", refresh: true, TestContext.Current.CancellationToken);

        fields.Value.ShouldNotBeNull().ShouldContain("i_Age");
        server.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task FieldsAsync_RefreshAfterForget_ReadsTheFieldsAgain()
    {
        var server = new FakeServer(_ => (HttpStatusCode.OK, """{"fields":[{"name":"i_Age"}]}"""));
        var client = Client(server);
        var schema = new TypesenseSchema(new TestOptionsMonitor<TypesenseOptions>(new TypesenseOptions()));

        await schema.FieldsAsync(client, "people", refresh: false, TestContext.Current.CancellationToken);
        schema.Forget(client, "people");
        await schema.FieldsAsync(client, "people", refresh: true, TestContext.Current.CancellationToken);

        server.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public void SortBy_IntegersAndFractionsInOneField_ReturnsFailedResult()
    {
        var result = TypesenseSearchProviderAsync.SortBy([("Price", false)], new HashSet<string> { "oi_Price", "on_Price", "i_Price", "n_Price" });

        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("holds an integer or date in some documents and a fractional number type");
    }

    [Fact]
    public void SortBy_OnlyIntegers_SortsOnTheIntegerCopy()
    {
        var result = TypesenseSearchProviderAsync.SortBy([("Price", true)], new HashSet<string> { "oi_Price", "i_Price" });

        result.Value.ShouldBe("oi_Price:desc");
    }

    [Fact]
    public async Task PingAsync_HealthOk_SendsGetHealthAndSucceeds()
    {
        var server = new FakeServer(_ => (HttpStatusCode.OK, """{"ok":true}"""));

        var result = await Search(server).PingAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var request = server.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.Uri.AbsolutePath.ShouldBe("/health");
    }

    [Fact]
    public async Task PingAsync_HealthNotOk_ReturnsFailedResultWithTheAnswer()
    {
        var server = new FakeServer(_ => (HttpStatusCode.OK, """{"ok":false}"""));

        var result = await Search(server).PingAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("""Typesense ping failed: it reported its health as {"ok":false}, not {"ok":true}.""");
    }

    [Fact]
    public async Task PingAsync_EveryNodeAnswers503_ReturnsFailedResultWithTheLastAnswer()
    {
        var server = new FakeServer(_ => (HttpStatusCode.ServiceUnavailable, """{"ok":false,"resource_error":"OUT_OF_DISK"}"""));

        var result = await Search(server).PingAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().Message.ShouldStartWith("Typesense ping failed: Typesense answered 503");
        server.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task PingAsync_FirstNodeUnreachable_SucceedsOnTheNextNode()
    {
        var server = new FakeServer(request => request.RequestUri!.Host == FirstNode.Host
            ? throw new HttpRequestException("connection refused")
            : (HttpStatusCode.OK, """{"ok":true}"""));

        var result = await Search(server).PingAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        server.Requests.Count.ShouldBe(2);
    }

    private static TypesenseHttpClientWrapper Client(FakeServer server) =>
        new(new HttpClient(server), [FirstNode, SecondNode], "key");

    private static TypesenseSearchProviderAsync Search(FakeServer server)
    {
        var options = new TestOptionsMonitor<TypesenseOptions>(new TypesenseOptions());
        return new TypesenseSearchProviderAsync(null!, new FixedClientFactory(Client(server)), new TypesenseSchema(options), options);
    }

    private static TypesenseIndexingProviderAsync Indexing(FakeServer server)
    {
        var options = new TestOptionsMonitor<TypesenseOptions>(new TypesenseOptions());
        return new TypesenseIndexingProviderAsync(new FixedClientFactory(Client(server)), options, new TypesenseSchema(options),
            NullLogger<TypesenseIndexingProviderAsync>.Instance);
    }

    private sealed class FixedClientFactory(TypesenseHttpClientWrapper client) : IClientFactoryAsync<TypesenseHttpClientWrapper>
    {
        public Task<TypesenseHttpClientWrapper> GetOrCreateClientAsync(string indexName) => Task.FromResult(client);
    }

    private sealed class FakeServer(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> answer) : HttpMessageHandler
    {
        public List<(HttpMethod Method, Uri Uri)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Requests)
            {
                Requests.Add((request.Method, request.RequestUri!));
            }

            var (status, body) = answer(request);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
