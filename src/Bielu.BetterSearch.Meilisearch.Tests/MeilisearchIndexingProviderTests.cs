using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Meilisearch.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using MeilisearchClient = Meilisearch.MeilisearchClient;
using MeilisearchMessageHandler = Meilisearch.MeilisearchMessageHandler;

namespace Bielu.BetterSearch.Meilisearch.Tests;

/// <summary>
/// Provider paths the shared <c>IndexingTestBase</c> does not reach: Core indexes many documents one by one,
/// so bulk indexing and removing every document are exercised here directly, along with the error paths.
/// </summary>
public sealed class MeilisearchIndexingProviderTests(
    IIndexingProviderAsync provider,
    IClientFactoryAsync<MeilisearchClient> clientFactory,
    IOptionsMonitor<MeilisearchOptions> options)
{
    private static string UniqueIndex() => $"meilisearch-provider-{Guid.NewGuid():N}";

    private static List<SearchDocument> CreateDocuments(string index, int count) =>
        Enumerable.Range(1, count)
            .Select(i => new SearchDocument
            {
                Id = i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Type = "TestDocument",
                Index = index,
                Fields = new Dictionary<string, IList<object>> { ["Name"] = [$"Name {i}"] },
            })
            .ToList();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<long> CountAsync(string index)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index);
        return (await client.Index(index).GetStatsAsync(Token)).NumberOfDocuments;
    }

    [Fact]
    public async Task IndexDocumentAsync_NewDocument_StoresItUnderItsId()
    {
        var index = UniqueIndex();
        var document = CreateDocuments(index, 1)[0];

        var result = await provider.IndexDocumentAsync(document, Token);

        result.IsSuccess.ShouldBeTrue();
        var client = await clientFactory.GetOrCreateClientAsync(index);
        var stored = await client.Index(index).GetDocumentAsync<SearchDocument>(document.Id, cancellationToken: Token);
        stored.Id.ShouldBe(document.Id);
        stored.Type.ShouldBe(document.Type);
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_DocumentsInOneIndex_IndexesEveryDocument()
    {
        var index = UniqueIndex();

        var result = await provider.IndexMultipleDocumentsAsync(CreateDocuments(index, 3), Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(3);
        (await CountAsync(index)).ShouldBe(3);
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_DocumentsInTwoIndexes_IndexesEachIndex()
    {
        var first = UniqueIndex();
        var second = UniqueIndex();

        var result = await provider.IndexMultipleDocumentsAsync([.. CreateDocuments(first, 2), .. CreateDocuments(second, 1)], Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(3);
        (await CountAsync(first)).ShouldBe(2);
        (await CountAsync(second)).ShouldBe(1);
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_SameIdTwice_KeepsOneDocument()
    {
        var index = UniqueIndex();
        (await provider.IndexMultipleDocumentsAsync(CreateDocuments(index, 2), Token)).IsSuccess.ShouldBeTrue();

        (await provider.IndexMultipleDocumentsAsync(CreateDocuments(index, 2), Token)).IsSuccess.ShouldBeTrue();

        (await CountAsync(index)).ShouldBe(2);
    }

    [Fact]
    public async Task IndexDocumentAsync_InvalidId_ReturnsFailureWithMeilisearchCode()
    {
        var document = new SearchDocument { Id = "not a valid id!", Type = "TestDocument", Index = UniqueIndex() };

        var result = await provider.IndexDocumentAsync(document, Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors[0].Metadata["code"].ShouldBe("invalid_document_id");
    }

    public static TheoryData<object> InvalidLocations() =>
    [
        "52.23",
        "north,south",
        "91,21",
        new Dictionary<string, object> { ["lat"] = 52.23 },
        new Dictionary<string, object> { ["lat"] = 52.23, ["lon"] = 181 },
        42,
    ];

    [Theory]
    [MemberData(nameof(InvalidLocations))]
    public async Task IndexDocumentAsync_InvalidLocation_ReturnsFailureWithoutCreatingTheIndex(object location)
    {
        var index = UniqueIndex();
        var document = new SearchDocument
        {
            Id = "1",
            Type = "TestDocument",
            Index = index,
            Fields = new Dictionary<string, IList<object>> { ["Location"] = [location] },
        };

        var result = await provider.IndexDocumentAsync(document, Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("Location");
        (await provider.IndexExistsAsync(index, Token)).Value.ShouldBeFalse();
    }

    [Fact]
    public async Task IndexDocumentAsync_TwoLocations_ReturnsFailure()
    {
        var document = new SearchDocument
        {
            Id = "1",
            Type = "TestDocument",
            Index = UniqueIndex(),
            Fields = new Dictionary<string, IList<object>> { ["Location"] = ["52.23,21.01", "50.06,19.94"] },
        };

        (await provider.IndexDocumentAsync(document, Token)).IsFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveDocumentAsync_ExistingDocument_RemovesIt()
    {
        var index = UniqueIndex();
        (await provider.IndexMultipleDocumentsAsync(CreateDocuments(index, 2), Token)).IsSuccess.ShouldBeTrue();

        var result = await provider.RemoveDocumentAsync("1", index, Token);

        result.IsSuccess.ShouldBeTrue();
        (await CountAsync(index)).ShouldBe(1);
    }

    [Fact]
    public async Task RemoveDocumentsAsync_TaskTimesOut_ReturnsFailureSayingTheTaskMayStillRemoveThem()
    {
        var index = UniqueIndex();
        try
        {
            (await provider.IndexMultipleDocumentsAsync(CreateDocuments(index, 3), Token)).Value.ShouldBe(3);
            // A timeout this short ends the wait before Meilisearch can finish the task.
            var timingOut = ProviderWith(o => o.TaskTimeout = TimeSpan.FromTicks(1));

            var result = await timingOut.RemoveDocumentsAsync(index, ["1", "2"], Token);

            var error = result.Errors.ShouldHaveSingleItem();
            error.Message.ShouldStartWith($"Removing documents from {index} did not finish: ");
            error.Message.ShouldEndWith(" was queued and may still remove any or all of the 2 documents.");
            error.Metadata["ids"].ShouldBe(new List<string> { "1", "2" });
            // The task was not cancelled, so Meilisearch still applies it.
            var client = await clientFactory.GetOrCreateClientAsync(index);
            var task = await client.WaitForTaskAsync((int)error.Metadata["taskUid"], cancellationToken: Token);
            task.Status.ShouldBe(global::Meilisearch.TaskInfoStatus.Succeeded);
            (await CountAsync(index)).ShouldBe(1);
        }
        finally
        {
            try
            {
                await provider.DeleteIndexAsync(index, CancellationToken.None);
            }
            catch (Exception)
            {
                // Cleanup must not hide the test's own failure.
            }
        }
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_SettingsTaskTimesOut_ReturnsFailureSayingNoneWereIndexed()
    {
        var index = UniqueIndex();
        try
        {
            // A new index needs its settings updated first; a timeout this short ends that wait before the
            // documents are sent.
            var timingOut = ProviderWith(o => o.TaskTimeout = TimeSpan.FromTicks(1));

            var result = await timingOut.IndexMultipleDocumentsAsync(CreateDocuments(index, 3), Token);

            var error = result.Errors.ShouldHaveSingleItem();
            error.Message.ShouldStartWith($"Indexing documents into {index} failed for ids 1, 2, 3: ");
            error.Message.ShouldEndWith(" 0 of 3 documents were indexed.");
            error.Metadata["ids"].ShouldBe(new List<string> { "1", "2", "3" });
            error.Metadata.ShouldNotContainKey("taskUid");
        }
        finally
        {
            try
            {
                await provider.DeleteIndexAsync(index, CancellationToken.None);
            }
            catch (Exception)
            {
                // Cleanup must not hide the test's own failure.
            }
        }
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_ConnectionDropsAfterTheAddIsSent_ReturnsFailureSayingTheyMayStillBeIndexed()
    {
        var index = UniqueIndex();
        var settings = options.CurrentValue.SharedIndexSettings;
        // Meilisearch receives the documents, and the answer is lost on the way back.
        var dropping = new DroppingDocumentsResponseHandler { InnerHandler = new HttpClientHandler() };
        var client = new MeilisearchClient(new HttpClient(new MeilisearchMessageHandler(dropping)) { BaseAddress = new Uri(settings.ConnectionString) }, settings.ApiKey);
        var droppingProvider = new MeilisearchIndexingProviderAsync(new FixedClientFactory(client), options,
            NullLogger<MeilisearchIndexingProviderAsync>.Instance);
        try
        {
            var result = await droppingProvider.IndexMultipleDocumentsAsync(CreateDocuments(index, 3), Token);

            var error = result.Errors.ShouldHaveSingleItem();
            error.Message.ShouldStartWith($"Indexing documents into {index} did not finish: ");
            error.Message.ShouldEndWith(" so it may still index any or all of the 3 documents.");
            error.Metadata["ids"].ShouldBe(new List<string> { "1", "2", "3" });
            dropping.Dropped.ShouldBeTrue();
        }
        finally
        {
            try
            {
                await provider.DeleteIndexAsync(index, CancellationToken.None);
            }
            catch (Exception)
            {
                // Cleanup must not hide the test's own failure.
            }
        }
    }

    private sealed class DroppingDocumentsResponseHandler : DelegatingHandler
    {
        public bool Dropped { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (request.Method != HttpMethod.Post || request.RequestUri?.AbsolutePath.EndsWith("/documents", StringComparison.Ordinal) != true)
            {
                return response;
            }

            response.Dispose();
            Dropped = true;
            throw new HttpRequestException("The connection was closed before the response arrived.");
        }
    }

    [Fact]
    public async Task RemoveAllDocumentsAsync_IndexWithDocuments_ReturnsDeletedCount()
    {
        var index = UniqueIndex();
        (await provider.IndexMultipleDocumentsAsync(CreateDocuments(index, 2), Token)).IsSuccess.ShouldBeTrue();

        var result = await provider.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = index }, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(2);
        (await CountAsync(index)).ShouldBe(0);
    }

    [Fact]
    public async Task RemoveAllDocumentsAsync_MissingIndex_ReturnsFailure()
    {
        var result = await provider.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = UniqueIndex() }, Token);

        result.IsFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task IndexExistsAsync_MissingIndex_ReturnsFalse()
    {
        var result = await provider.IndexExistsAsync(UniqueIndex(), Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task IndexExistsAsync_ExistingIndex_ReturnsTrue()
    {
        var index = UniqueIndex();
        (await provider.CreateIndexAsync(index, Token)).Value.ShouldBeTrue();

        var result = await provider.IndexExistsAsync(index, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_MissingIndex_CreatesIndex()
    {
        var index = UniqueIndex();

        var result = await provider.EnsureIndexExistsAsync(index, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
        (await provider.IndexExistsAsync(index, Token)).Value.ShouldBeTrue();
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_ExistingIndex_ReturnsTrue()
    {
        var index = UniqueIndex();
        (await provider.CreateIndexAsync(index, Token)).Value.ShouldBeTrue();

        var result = await provider.EnsureIndexExistsAsync(index, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_ConcurrentCalls_AllReturnTrue()
    {
        var index = UniqueIndex();

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => provider.EnsureIndexExistsAsync(index, Token)));

        results.ShouldAllBe(r => r.IsSuccess && r.Value);
    }

    private MeilisearchIndexingProviderAsync ProviderWith(Action<MeilisearchOptions> configure)
    {
        var changed = new MeilisearchOptions { SharedIndexSettings = options.CurrentValue.SharedIndexSettings };
        configure(changed);
        return new MeilisearchIndexingProviderAsync(clientFactory, new StaticOptionsMonitor(changed),
            NullLogger<MeilisearchIndexingProviderAsync>.Instance);
    }

    private async Task UpdateSettingsElsewhereAsync(string index, global::Meilisearch.Settings settings)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index);
        var task = await client.WaitForTaskAsync((await client.Index(index).UpdateSettingsAsync(settings, Token)).TaskUid, cancellationToken: Token);
        task.Status.ShouldBe(global::Meilisearch.TaskInfoStatus.Succeeded);
    }

    private async Task<global::Meilisearch.Settings> SettingsAsync(string index)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index);
        return await client.Index(index).GetSettingsAsync(Token);
    }

    [Fact]
    public async Task IndexDocumentAsync_IndexDeletedElsewhere_AppliesTheSettingsAgain()
    {
        var index = UniqueIndex();
        var uncached = ProviderWith(o => o.SettingsCheckInterval = TimeSpan.Zero);
        (await uncached.IndexDocumentAsync(CreateDocuments(index, 1)[0], Token)).IsSuccess.ShouldBeTrue();
        var client = await clientFactory.GetOrCreateClientAsync(index);
        await client.WaitForTaskAsync((await client.DeleteIndexAsync(index, Token)).TaskUid, cancellationToken: Token);

        var result = await uncached.IndexDocumentAsync(CreateDocuments(index, 1)[0], Token);

        result.IsSuccess.ShouldBeTrue();
        var settings = await SettingsAsync(index);
        settings.FilterableAttributes!.Select(attribute => attribute.Attribute).ShouldContain("tokens");
        settings.Pagination!.MaxTotalHits.ShouldBe(options.CurrentValue.MaxTotalHits);
    }

    [Fact]
    public async Task CreateIndexAsync_IndexDeletedElsewhereWithinTheInterval_AppliesTheSettings()
    {
        var index = UniqueIndex();
        var cached = ProviderWith(o => o.SettingsCheckInterval = TimeSpan.FromHours(1));
        (await cached.IndexDocumentAsync(CreateDocuments(index, 1)[0], Token)).IsSuccess.ShouldBeTrue();
        var client = await clientFactory.GetOrCreateClientAsync(index);
        await client.WaitForTaskAsync((await client.DeleteIndexAsync(index, Token)).TaskUid, cancellationToken: Token);

        var result = await cached.CreateIndexAsync(index, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
        (await SettingsAsync(index)).FilterableAttributes!.Select(attribute => attribute.Attribute).ShouldContain("tokens");
    }

    [Fact]
    public async Task IndexDocumentAsync_IndexDeletedThroughTheProvider_AppliesTheSettingsAgain()
    {
        var index = UniqueIndex();
        (await provider.IndexDocumentAsync(CreateDocuments(index, 1)[0], Token)).IsSuccess.ShouldBeTrue();
        (await provider.DeleteIndexAsync(index, Token)).Value.ShouldBeTrue();

        (await provider.IndexDocumentAsync(CreateDocuments(index, 1)[0], Token)).IsSuccess.ShouldBeTrue();

        (await SettingsAsync(index)).FilterableAttributes!.Select(attribute => attribute.Attribute).ShouldContain("tokens");
    }

    [Fact]
    public async Task IndexDocumentAsync_SettingsCheckedWithinTheInterval_DoesNotReadThemAgain()
    {
        var index = UniqueIndex();
        var cached = ProviderWith(o => o.SettingsCheckInterval = TimeSpan.FromHours(1));
        (await cached.IndexDocumentAsync(CreateDocuments(index, 1)[0], Token)).IsSuccess.ShouldBeTrue();
        await UpdateSettingsElsewhereAsync(index, new global::Meilisearch.Settings { FilterableAttributes = [new global::Meilisearch.FilterableAttribute { Attribute = "id" }] });

        (await cached.IndexDocumentAsync(CreateDocuments(index, 1)[0], Token)).IsSuccess.ShouldBeTrue();
        (await SettingsAsync(index)).FilterableAttributes!.Select(attribute => attribute.Attribute).ShouldNotContain("tokens");

        (await ProviderWith(o => o.SettingsCheckInterval = TimeSpan.Zero).IndexDocumentAsync(CreateDocuments(index, 1)[0], Token)).IsSuccess.ShouldBeTrue();
        (await SettingsAsync(index)).FilterableAttributes!.Select(attribute => attribute.Attribute).ShouldContain("tokens");
    }

    [Fact]
    public async Task IndexDocumentAsync_MaxTotalHitsRaised_UpdatesThePagination()
    {
        var index = UniqueIndex();
        (await provider.IndexDocumentAsync(CreateDocuments(index, 1)[0], Token)).IsSuccess.ShouldBeTrue();

        (await ProviderWith(o => o.MaxTotalHits = 20_000).IndexDocumentAsync(CreateDocuments(index, 1)[0], Token)).IsSuccess.ShouldBeTrue();

        (await SettingsAsync(index)).Pagination!.MaxTotalHits.ShouldBe(20_000);
    }

    [Fact]
    public async Task IndexDocumentAsync_MaxTotalHitsLowerThanTheIndex_KeepsTheHigherValue()
    {
        var index = UniqueIndex();
        (await ProviderWith(o => o.MaxTotalHits = 20_000).IndexDocumentAsync(CreateDocuments(index, 1)[0], Token)).IsSuccess.ShouldBeTrue();

        (await ProviderWith(o => { o.MaxTotalHits = 500; o.SettingsCheckInterval = TimeSpan.Zero; })
            .IndexDocumentAsync(CreateDocuments(index, 1)[0], Token)).IsSuccess.ShouldBeTrue();

        (await SettingsAsync(index)).Pagination!.MaxTotalHits.ShouldBe(20_000);
    }

    [Fact]
    public async Task IndexDocumentAsync_AttributesAddedElsewhere_KeepsThem()
    {
        var index = UniqueIndex();
        (await provider.IndexDocumentAsync(CreateDocuments(index, 1)[0], Token)).IsSuccess.ShouldBeTrue();
        await UpdateSettingsElsewhereAsync(index, new global::Meilisearch.Settings
        {
            FilterableAttributes = [new global::Meilisearch.FilterableAttribute { Attribute = "custom" }],
            SortableAttributes = ["custom"],
        });

        (await ProviderWith(o => o.SettingsCheckInterval = TimeSpan.Zero).IndexDocumentAsync(CreateDocuments(index, 1)[0], Token)).IsSuccess.ShouldBeTrue();

        var settings = await SettingsAsync(index);
        settings.FilterableAttributes!.Select(attribute => attribute.Attribute).ShouldContain("custom");
        settings.FilterableAttributes!.Select(attribute => attribute.Attribute).ShouldContain("tokens");
        settings.SortableAttributes!.ShouldBe(["custom", "fields"], ignoreOrder: true);
    }

    [Fact]
    public void SettingsCheckInterval_Default_IsFiveSeconds()
    {
        new MeilisearchOptions().SettingsCheckInterval.ShouldBe(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task IndexDocumentAsync_PhraseFieldsSet_StoresPhrasesForThoseFieldsOnly()
    {
        var index = UniqueIndex();
        var restricted = new MeilisearchOptions
        {
            SharedIndexSettings = options.CurrentValue.SharedIndexSettings,
            PhraseFields = ["Title"],
        };
        var other = new MeilisearchIndexingProviderAsync(clientFactory, new StaticOptionsMonitor(restricted),
            NullLogger<MeilisearchIndexingProviderAsync>.Instance);
        var document = CreateDocuments(index, 1)[0];
        document.Fields["Title"] = ["Jane Doe"];

        (await other.IndexDocumentAsync(document, Token)).IsSuccess.ShouldBeTrue();

        var client = await clientFactory.GetOrCreateClientAsync(index);
        var stored = await client.Index(index).GetDocumentAsync<MeilisearchDocument>(document.Id, cancellationToken: Token);
        stored.Phrases.Keys.ShouldBe(["Title"]);
        stored.Tokens.Keys.ShouldBe(["Name", "Title"], ignoreOrder: true);
    }

    [Fact]
    public async Task CreateIndexAsync_IndexAlreadyExists_ReturnsFalse()
    {
        var index = UniqueIndex();
        (await provider.CreateIndexAsync(index, Token)).Value.ShouldBeTrue();

        var result = await provider.CreateIndexAsync(index, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateIndexAsync_InvalidName_ReturnsFailure()
    {
        var result = await provider.CreateIndexAsync("not a valid index!", Token);

        result.IsFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteIndexAsync_ExistingIndex_RemovesIt()
    {
        var index = UniqueIndex();
        (await provider.CreateIndexAsync(index, Token)).Value.ShouldBeTrue();

        var result = await provider.DeleteIndexAsync(index, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
        (await provider.IndexExistsAsync(index, Token)).Value.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteIndexAsync_MissingIndex_ReturnsFailure()
    {
        var result = await provider.DeleteIndexAsync(UniqueIndex(), Token);

        result.IsFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task IndexExistsAsync_WrongApiKey_ReturnsFailure()
    {
        var wrongKey = CreateProvider(options.CurrentValue.SharedIndexSettings.ConnectionString, "wrong-key");

        var result = await wrongKey.IndexExistsAsync(UniqueIndex(), Token);

        result.IsFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateIndexAsync_UnreachableServer_ReturnsFailure()
    {
        var unreachable = CreateProvider("http://127.0.0.1:1", null);

        var result = await unreachable.CreateIndexAsync(UniqueIndex(), Token);

        result.IsFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_ExistsCheckFails_ReturnsTheCheckFailureWithoutCreating()
    {
        // Every request gets the same 401; a create attempt would be a second request.
        var handler = new UnauthorizedHandler();
        var provider = new MeilisearchIndexingProviderAsync(
            new FixedClientFactory(new MeilisearchClient(new HttpClient(new MeilisearchMessageHandler(handler)) { BaseAddress = new Uri("http://127.0.0.1:7700/") }, "wrong-key")),
            options,
            NullLogger<MeilisearchIndexingProviderAsync>.Instance);

        var result = await provider.EnsureIndexExistsAsync(UniqueIndex(), Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("The provided API key is invalid.");
        handler.Methods.ShouldBe([HttpMethod.Get]);
    }

    private sealed class FixedClientFactory(MeilisearchClient client) : IClientFactoryAsync<MeilisearchClient>
    {
        public Task<MeilisearchClient> GetOrCreateClientAsync(string indexName) => Task.FromResult(client);
    }

    private sealed class UnauthorizedHandler : HttpMessageHandler
    {
        public List<HttpMethod> Methods { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(
                    """{"message":"The provided API key is invalid.","code":"invalid_api_key","type":"auth","link":"https://docs.meilisearch.com/errors#invalid_api_key"}""",
                    System.Text.Encoding.UTF8,
                    "application/json"),
            });
        }
    }

    private static MeilisearchIndexingProviderAsync CreateProvider(string connectionString, string? apiKey)
    {
        var monitor = new StaticOptionsMonitor(new MeilisearchOptions
        {
            SharedIndexSettings = new MeilisearchIndexSettings { ConnectionString = connectionString, ApiKey = apiKey },
        });
        return new MeilisearchIndexingProviderAsync(
            new MeilisearchClientFactory(new MeilisearchClientSettingsManager(monitor)),
            monitor,
            NullLogger<MeilisearchIndexingProviderAsync>.Instance);
    }
}
