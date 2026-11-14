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

    // The Typesense provider does not implement facets yet.
    protected override bool HasFacets => false;

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

    protected override string? OverlappingCallsSkipReason =>
        "Typesense rebuilds have a single-writer contract (see the Typesense provider page): calls for one index must not overlap.";

    // Only the overlapping-call tests use it, and they are skipped.
    protected override Task BeginAbortAsync(IndexRebuild rebuild) => throw new NotSupportedException(OverlappingCallsSkipReason);

    /// <summary>A rebuild of <paramref name="index"/> filled with the document <paramref name="id"/>.</summary>
    private async Task<IndexRebuild> FilledRebuildAsync(IIndexLifecycleServiceAsync lifecycle, string index, string id)
    {
        var rebuild = await lifecycle.StartRebuildAsync(index, Token);
        ShouldSucceed(rebuild);
        ShouldSucceed(await lifecycle.IndexRebuildDocumentsAsync(rebuild.Value, [Document(index, id)], Token));
        return rebuild.Value;
    }

    private async Task CleanUpAsync(IReadOnlyList<string> indexes, IReadOnlyList<IndexRebuild> rebuilds)
    {
        try
        {
            foreach (var index in indexes)
            {
                await DeleteIndexAsync(index);
            }

            foreach (var rebuild in rebuilds)
            {
                await SendAsync(HttpMethod.Delete, $"collections/{Uri.EscapeDataString(rebuild.TargetIndex)}");
            }
        }
        catch (Exception)
        {
            // Cleanup must not hide the test's own failure.
        }
    }

    [Fact]
    public async Task CompleteRebuild_CalledAgainAfterTheAliasMoved_DeletesTheCollectionLeftBehind()
    {
        var lifecycle = ScopedServices.GetRequiredService<IIndexLifecycleServiceAsync>();
        var index = $"leftover-{Guid.NewGuid():N}";
        var rebuilds = new List<IndexRebuild>();
        ShouldSucceed(await Provider.EnsureIndexExistsAsync(index, Token));
        try
        {
            var first = await FilledRebuildAsync(lifecycle, index, "first");
            rebuilds.Add(first);
            ShouldSucceed(await lifecycle.CompleteRebuildAsync(first, Token));
            var second = await FilledRebuildAsync(lifecycle, index, "second");
            rebuilds.Add(second);
            // A complete that moved the alias and stopped before deleting the collection it pointed at before.
            var aliased = await SendAsync(HttpMethod.Put, $"aliases/{Uri.EscapeDataString(Prefix + index)}", $$"""{"collection_name":"{{second.TargetIndex}}"}""");
            aliased.IsSuccess.ShouldBeTrue(aliased.Body);

            var again = await lifecycle.CompleteRebuildAsync(second, Token);

            ShouldSucceed(again);
            again.Value.ShouldBe([first.TargetIndex]);
            (await EngineIndexExistsAsync(first.TargetIndex)).ShouldBeFalse();
            (await AliasTargetsAsync(Prefix + index)).ShouldBe([second.TargetIndex]);
            (await IdsAsync(index)).ShouldBe(["second"]);
        }
        finally
        {
            await CleanUpAsync([index], rebuilds);
        }
    }

    [Fact]
    public async Task CompleteRebuild_EarlierRebuildNeverCompletedOrAborted_DeletesItsCollection()
    {
        var lifecycle = ScopedServices.GetRequiredService<IIndexLifecycleServiceAsync>();
        var index = $"abandoned-{Guid.NewGuid():N}";
        var rebuilds = new List<IndexRebuild>();
        ShouldSucceed(await Provider.EnsureIndexExistsAsync(index, Token));
        try
        {
            var abandoned = await FilledRebuildAsync(lifecycle, index, "abandoned");
            rebuilds.Add(abandoned);
            var rebuild = await FilledRebuildAsync(lifecycle, index, "new");
            rebuilds.Add(rebuild);

            var completed = await lifecycle.CompleteRebuildAsync(rebuild, Token);

            ShouldSucceed(completed);
            completed.Value.ShouldBe([Prefix + index, abandoned.TargetIndex]);
            (await EngineIndexExistsAsync(abandoned.TargetIndex)).ShouldBeFalse();
            (await AliasTargetsAsync(Prefix + index)).ShouldBe([rebuild.TargetIndex]);
            (await IdsAsync(index)).ShouldBe(["new"]);
        }
        finally
        {
            await CleanUpAsync([index], rebuilds);
        }
    }

    [Fact]
    public async Task CompleteRebuild_OtherIndexWhoseNameStartsTheSame_LeavesItsCollectionsAlone()
    {
        var lifecycle = ScopedServices.GetRequiredService<IIndexLifecycleServiceAsync>();
        var index = $"same-start-{Guid.NewGuid():N}";
        // Names that start with the index's name, and with its rebuild collections' name.
        var longer = index + "-more";
        var lookalike = index + "-rebuild-later";
        var rebuilds = new List<IndexRebuild>();
        ShouldSucceed(await Provider.EnsureIndexExistsAsync(index, Token));
        ShouldSucceed(await Provider.EnsureIndexExistsAsync(lookalike, Token));
        try
        {
            var other = await FilledRebuildAsync(lifecycle, longer, "other");
            rebuilds.Add(other);
            var rebuild = await FilledRebuildAsync(lifecycle, index, "new");
            rebuilds.Add(rebuild);

            var completed = await lifecycle.CompleteRebuildAsync(rebuild, Token);

            ShouldSucceed(completed);
            completed.Value.ShouldBe([Prefix + index]);
            (await EngineIndexExistsAsync(other.TargetIndex)).ShouldBeTrue();
            (await EngineIndexExistsAsync(Prefix + lookalike)).ShouldBeTrue();
        }
        finally
        {
            await CleanUpAsync([index, lookalike], rebuilds);
        }
    }

    [Fact]
    public async Task AbortRebuild_AfterANewerRebuildCompleted_ReturnsFalseAndKeepsTheNewerOne()
    {
        var lifecycle = ScopedServices.GetRequiredService<IIndexLifecycleServiceAsync>();
        var index = $"abort-late-{Guid.NewGuid():N}";
        var rebuilds = new List<IndexRebuild>();
        ShouldSucceed(await Provider.EnsureIndexExistsAsync(index, Token));
        try
        {
            var first = await FilledRebuildAsync(lifecycle, index, "first");
            rebuilds.Add(first);
            ShouldSucceed(await lifecycle.CompleteRebuildAsync(first, Token));
            var second = await FilledRebuildAsync(lifecycle, index, "second");
            rebuilds.Add(second);
            ShouldSucceed(await lifecycle.CompleteRebuildAsync(second, Token));

            var aborted = await lifecycle.AbortRebuildAsync(first, Token);

            ShouldSucceed(aborted);
            aborted.Value.ShouldBeFalse();
            (await AliasTargetsAsync(Prefix + index)).ShouldBe([second.TargetIndex]);
            (await IdsAsync(index)).ShouldBe(["second"]);
            var abortedLive = await lifecycle.AbortRebuildAsync(second, Token);
            Errors(abortedLive).ShouldContain("already complete");
            (await EngineIndexExistsAsync(second.TargetIndex)).ShouldBeTrue();
        }
        finally
        {
            await CleanUpAsync([index], rebuilds);
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
    }

    protected override void Dispose(bool disposing) => (Services as IDisposable)?.Dispose();
}
