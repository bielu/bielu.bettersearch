using System.Net;
using System.Text;
using System.Text.Json;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.Tests;
using Bielu.BetterSearch.Typesense.Configuration;
using Bielu.BetterSearch.Typesense.DepedencyInjection;
using Bielu.BetterSearch.Typesense.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bielu.BetterSearch.Typesense.Tests;

/// <summary>The index prefix and rebuilds through collection aliases against a real Typesense.</summary>
public sealed class TypesenseIndexLifecycleTests(TypesenseResource typesense) : IndexLifecycleTestBase(CreateServices(typesense))
{
    private const string IndexPrefix = "lifecycle-test-";

    protected override string Prefix => IndexPrefix;

    // The Typesense provider does not implement facets, GetDocumentAsync, CountAsync or UpdateDocumentAsync yet.
    protected override bool HasFacets => false;

    protected override bool HasGetCountAndUpdate => false;

    private static ServiceProvider CreateServices(TypesenseResource typesense)
    {
        var services = new ServiceCollection();
        services.AddLogging().AddBetterSearch(x => x.AddTypesense());
        services.AddOptions<TypesenseOptions>().Configure(options =>
        {
            options.IndexPrefix = IndexPrefix;
            options.SharedIndexSettings = new TypesenseIndexSettings
            {
                ConnectionString = typesense.EnsureStarted().ToString(),
                ApiKey = typesense.ApiKey,
            };
        });
        return services.BuildServiceProvider();
    }

    private async Task<TypesenseResponse> SendAsync(HttpMethod method, string path, string? body = null)
    {
        var client = await Services.GetRequiredService<IClientFactoryAsync<TypesenseHttpClientWrapper>>().GetOrCreateClientAsync("any");
        return await client.SendAsync(method, path, body is null ? null : () => new StringContent(body, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
    }

    // A collection request through an alias reaches the collection behind it, so only the list of collections tells them apart.
    protected override async Task<bool> EngineIndexExistsAsync(string engineName)
    {
        var response = await SendAsync(HttpMethod.Get, "collections");
        response.IsSuccess.ShouldBeTrue(response.Body);
        return JsonDocument.Parse(response.Body).RootElement.EnumerateArray().Any(collection => collection.GetProperty("name").GetString() == engineName);
    }

    protected override async Task<IReadOnlyList<string>> AliasTargetsAsync(string alias)
    {
        var response = await SendAsync(HttpMethod.Get, $"aliases/{Uri.EscapeDataString(alias)}");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.IsSuccess.ShouldBeTrue(response.Body);
        return [JsonDocument.Parse(response.Body).RootElement.GetProperty("collection_name").GetString()!];
    }

    // An abort first records its decision by creating the rebuild's decision collection.
    protected override async Task BeginAbortAsync(IndexRebuild rebuild)
    {
        var response = await SendAsync(HttpMethod.Post, "collections",
            $$"""{"name":"{{TypesenseIndexingProviderAsync.DecisionCollection(rebuild.TargetIndex)}}","fields":[{"name":"abort","type":"string","optional":true}]}""");
        response.StatusCode.ShouldBe(HttpStatusCode.Created, response.Body);
    }

    [Fact]
    public async Task CompleteRebuild_EarlierCompleteStoppedBeforeMovingTheAlias_MovesItAfterWaiting()
    {
        var lifecycle = ScopedServices.GetRequiredService<IIndexLifecycleServiceAsync>();
        var index = $"stopped-{Guid.NewGuid():N}";
        ShouldSucceed(await Provider.EnsureIndexExistsAsync(index, Token));
        try
        {
            var rebuild = await lifecycle.StartRebuildAsync(index, Token);
            ShouldSucceed(rebuild);
            ShouldSucceed(await lifecycle.IndexRebuildDocumentsAsync(rebuild.Value, [Document(index, "new")], Token));
            // A complete that recorded its decision and then stopped.
            var decided = await SendAsync(HttpMethod.Post, "collections",
                $$"""{"name":"{{TypesenseIndexingProviderAsync.DecisionCollection(rebuild.Value.TargetIndex)}}","fields":[{"name":"complete","type":"string","optional":true}]}""");
            decided.StatusCode.ShouldBe(HttpStatusCode.Created, decided.Body);

            var completed = await lifecycle.CompleteRebuildAsync(rebuild.Value, Token);

            ShouldSucceed(completed);
            completed.Value.ShouldBe([Prefix + index]);
            (await AliasTargetsAsync(Prefix + index)).ShouldBe([rebuild.Value.TargetIndex]);
            (await IdsAsync(index)).ShouldBe(["new"]);
            var aborted = await lifecycle.AbortRebuildAsync(rebuild.Value, Token);
            Errors(aborted).ShouldContain("already complete");
        }
        finally
        {
            await DeleteIndexAsync(index);
        }
    }

    [Fact]
    public async Task IndexRebuildDocuments_Aborted_FailsAndDoesNotCreateTheRebuildIndex()
    {
        var lifecycle = ScopedServices.GetRequiredService<IIndexLifecycleServiceAsync>();
        var index = $"aborted-{Guid.NewGuid():N}";
        var rebuild = await lifecycle.StartRebuildAsync(index, Token);
        ShouldSucceed(rebuild);
        ShouldSucceed(await lifecycle.AbortRebuildAsync(rebuild.Value, Token));

        var indexed = await lifecycle.IndexRebuildDocumentsAsync(rebuild.Value, [Document(index, "late")], Token);

        indexed.IsFailed.ShouldBeTrue();
        Errors(indexed).ShouldContain($"rebuild index {rebuild.Value.TargetIndex}");
        (await EngineIndexExistsAsync(rebuild.Value.TargetIndex)).ShouldBeFalse();
        (await EngineIndexExistsAsync(TypesenseIndexingProviderAsync.DecisionCollection(rebuild.Value.TargetIndex))).ShouldBeFalse();
    }

    protected override void Dispose(bool disposing) => (Services as IDisposable)?.Dispose();
}
