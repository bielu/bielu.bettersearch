using Bielu.BetterSearch.Abstractions.Caching;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Caching.FusionCache;
using Bielu.BetterSearch.DepedencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Bielu.BetterSearch.Caching.Tests;

/// <summary>
/// Two nodes, each with its own FusionCache, sharing one distributed (L2) cache and one backplane, as two instances
/// of an application behind Redis would.
/// </summary>
public sealed class FusionSearchResultCacheTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly IDistributedCache _distributed = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    private readonly string _connectionId = Guid.NewGuid().ToString("N");
    private readonly List<IDisposable> _disposables = [];

    private (ServiceProvider Services, CountingSearchProvider Search) Node()
    {
        var fusion = new ZiggyCreatures.Caching.Fusion.FusionCache(new FusionCacheOptions { CacheName = "search" });
        fusion.SetupSerializer(new FusionCacheSystemTextJsonSerializer());
        fusion.SetupDistributedCache(_distributed);
        fusion.SetupBackplane(new MemoryBackplane(new MemoryBackplaneOptions { ConnectionId = _connectionId }));

        var search = new CountingSearchProvider();
        var services = new ServiceCollection();
        services.AddSingleton<IFusionCache>(fusion);
        services.AddSingleton<ISearchProviderAsync>(search);
        services.AddSingleton<IIndexingProviderAsync>(new FakeIndexingProvider());
        services.AddBetterSearch(c => c.AddFusionSearchCache());
        var provider = services.BuildServiceProvider();
        _disposables.Add(fusion);
        _disposables.Add(provider);
        return (provider, search);
    }

    private static async Task<ISearchResult> SearchAsync(ServiceProvider services, ISearchQuery query)
    {
        using var scope = services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISearchServiceAsync>().SearchAsync(query, Token);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    [Fact]
    public async Task IndexingWriteOnNodeA_EvictsTheEntryOnNodeB()
    {
        //Arrange
        var (a, searchA) = Node();
        var (b, searchB) = Node();
        (await SearchAsync(a, Queries.Term("products"))).Items.Single().Document.Id.ShouldBe("products-1");
        (await SearchAsync(b, Queries.Term("products"))).Items.Single().Document.Id.ShouldBe("products-1");
        searchA.Calls.ShouldBe(1);
        searchB.Calls.ShouldBe(0);

        //Act
        using (var scope = a.CreateScope())
        {
            (await scope.ServiceProvider.GetRequiredService<IIndexingServiceAsync>()
                .IndexDocumentAsync(new SearchDocument { Id = "1", Index = "products" }, Token)).IsSuccess.ShouldBeTrue();
        }

        // The backplane delivers the eviction asynchronously, so give it a moment.
        ISearchResult? result = null;
        for (var attempt = 0; attempt < 50 && searchB.Calls == 0; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), Token);
            result = await SearchAsync(b, Queries.Term("products"));
        }

        //Assert
        searchB.Calls.ShouldBe(1);
        result!.Items.Single().Document.Id.ShouldBe("products-1");
        searchA.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task SearchAsync_SameQueryTwice_CallsProviderOnce()
    {
        //Arrange
        var (a, search) = Node();

        //Act
        await SearchAsync(a, Queries.Term("products"));
        var second = await SearchAsync(a, Queries.Term("products"));

        //Assert
        search.Calls.ShouldBe(1);
        second.Items.Single().Document.CustomProperties["call"].ShouldBe([1L]);
    }

    [Fact]
    public async Task SearchAsync_ProviderFails_ReturnsFailureAndCachesNothing()
    {
        //Arrange
        var (a, search) = Node();
        search.FailWith = "engine down";

        //Act
        ISearchResult? second;
        using (var scope = a.CreateScope())
        {
            var first = await scope.ServiceProvider.GetRequiredService<ISearchServiceAsync>().SearchAsync(Queries.Term("products"), Token);
            first.IsFailed.ShouldBeTrue();
            first.Errors.Single().Message.ShouldBe("engine down");
        }

        search.FailWith = null;
        second = await SearchAsync(a, Queries.Term("products"));

        //Assert
        search.Calls.ShouldBe(2);
        second.Items.Single().Document.Id.ShouldBe("products-2");
    }

    [Fact]
    public async Task SearchAsync_CancelledWhileSearching_Throws()
    {
        //Arrange
        var (a, search) = Node();
        using var cancellation = new CancellationTokenSource();
        search.During = async token =>
        {
            await cancellation.CancelAsync();
            token.ThrowIfCancellationRequested();
        };
        using var scope = a.CreateScope();

        //Act
        var act = () => scope.ServiceProvider.GetRequiredService<ISearchServiceAsync>().SearchAsync(Queries.Term("products"), cancellation.Token);

        //Assert
        await Should.ThrowAsync<OperationCanceledException>(act);
    }

    [Fact]
    public void AddFusionSearchCache_RegistersAdapter()
    {
        var (a, _) = Node();
        a.GetRequiredService<ISearchResultCache>().ShouldBeOfType<FusionSearchResultCache>();
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
    }
}
