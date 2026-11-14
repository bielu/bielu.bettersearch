using Bielu.BetterSearch.Abstractions.Caching;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.Tests;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bielu.BetterSearch.Caching.Tests;

/// <summary>
/// The search cache through the services, which reach it through the provider decorators, with a counting fake provider
/// and the built-in <see cref="MemorySearchResultCache"/>; no provider changes, so every provider behaves this way.
/// </summary>
public class SearchServiceCachingTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly CountingSearchProvider _search = new();
    private readonly FakeIndexingProvider _indexing = new();

    private ServiceProvider Build(Action<Configurator>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISearchProviderAsync>(_search);
        services.AddSingleton<IIndexingProviderAsync>(_indexing);
        services.AddBetterSearch(configurator => (configure ?? (c => c.AddMemorySearchCache()))(configurator));
        return services.BuildServiceProvider();
    }

    private static async Task<ISearchResult> SearchAsync(ServiceProvider services, ISearchQuery query)
    {
        using var scope = services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISearchServiceAsync>().SearchAsync(query, Token);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private static IIndexingServiceAsync Indexing(ServiceProvider services) =>
        services.CreateScope().ServiceProvider.GetRequiredService<IIndexingServiceAsync>();

    [Fact]
    public async Task SearchAsync_SameQueryTwice_CallsProviderOnceAndReturnsSameHits()
    {
        //Arrange
        await using var services = Build();

        //Act
        var first = await SearchAsync(services, Queries.Term("products"));
        var second = await SearchAsync(services, Queries.Term("products"));

        //Assert
        _search.Calls.ShouldBe(1);
        second.Items.Single().Document.Id.ShouldBe("products-1");
        second.Items.Single().Document.CustomProperties["call"].ShouldBe([1L]);
        first.Items.Single().Document.Id.ShouldBe("products-1");
        second.Items.Single().Score.ShouldBe(1.5);
    }

    [Fact]
    public async Task SearchAsync_NoCacheRegistered_CallsProviderEveryTime()
    {
        //Arrange
        await using var services = Build(_ => { });

        //Act
        await SearchAsync(services, Queries.Term("products"));
        var second = await SearchAsync(services, Queries.Term("products"));

        //Assert
        _search.Calls.ShouldBe(2);
        second.Items.Single().Document.Id.ShouldBe("products-2");
        services.GetService<ISearchResultCache>().ShouldBeNull();
    }

    public static TheoryData<string, SearchQuery> DifferentQueries => new()
    {
        { "page", Queries.Term("products", page: 2) },
        { "boost", Queries.Term("products", boost: 3) },
        { "sort direction", Queries.Term("products", desc: true) },
        { "value", Queries.Term("products", value: "john") },
        { "index", Queries.Term("articles") }
    };

    [Theory]
    [MemberData(nameof(DifferentQueries))]
    public async Task SearchAsync_QueryDiffersInOneSetting_CallsProviderAgain(string setting, SearchQuery different)
    {
        //Arrange
        await using var services = Build();
        await SearchAsync(services, Queries.Term("products"));

        //Act
        var result = await SearchAsync(services, different);

        //Assert
        _search.Calls.ShouldBe(2, setting);
        result.Items.Single().Document.Id.ShouldEndWith("-2");
    }

    public static TheoryData<string> Writes => ["index", "index many", "remove", "remove many", "remove all"];

    private static Task WriteAsync(IIndexingServiceAsync indexing, string write, string index) => write switch
    {
        "index" => indexing.IndexDocumentAsync(new SearchDocument { Id = "1", Index = index }, Token),
        "index many" => indexing.IndexMultipleDocumentsAsync([new SearchDocument { Id = "1", Index = index }], Token),
        "remove" => indexing.RemoveDocumentAsync(new DeleteDocumentRequest { Id = "1", Type = index }, Token),
        "remove many" => indexing.RemoveDocumentsAsync(index, ["1"], Token),
        "remove all" => indexing.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = index }, Token),
        _ => throw new ArgumentOutOfRangeException(nameof(write))
    };

    [Theory]
    [MemberData(nameof(Writes))]
    public async Task IndexingWrite_SameIndex_EvictsCachedSearches(string write)
    {
        //Arrange
        await using var services = Build();
        await SearchAsync(services, Queries.Term("products"));

        //Act
        // A different case on purpose: index names compare ignoring case.
        await WriteAsync(Indexing(services), write, "Products");
        var result = await SearchAsync(services, Queries.Term("products"));

        //Assert
        _search.Calls.ShouldBe(2);
        result.Items.Single().Document.Id.ShouldBe("products-2");
    }

    [Theory]
    [MemberData(nameof(Writes))]
    public async Task IndexingWrite_OtherIndex_KeepsCachedSearches(string write)
    {
        //Arrange
        await using var services = Build();
        await SearchAsync(services, Queries.Term("products"));

        //Act
        await WriteAsync(Indexing(services), write, "articles");
        var result = await SearchAsync(services, Queries.Term("products"));

        //Assert
        _search.Calls.ShouldBe(1);
        result.Items.Single().Document.Id.ShouldBe("products-1");
    }

    [Fact]
    public async Task IndexingWrite_ProviderFails_StillEvicts()
    {
        //Arrange
        await using var services = Build();
        await SearchAsync(services, Queries.Term("products"));
        _indexing.Fail = true;

        //Act
        var write = await Indexing(services).RemoveDocumentsAsync("products", ["1"], Token);
        await SearchAsync(services, Queries.Term("products"));

        //Assert
        write.IsFailed.ShouldBeTrue();
        _search.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task IndexingWrite_EvictionThrows_ReturnsWriteResult()
    {
        //Arrange
        await using var services = Build(c => c.AddSearchCache<ThrowingCache>());

        //Act
        var write = await Indexing(services).RemoveDocumentsAsync("products", ["1", "2"], Token);

        //Assert
        write.IsSuccess.ShouldBeTrue();
        write.Value.ShouldBe(2);
    }

    [Fact]
    public async Task SearchAsync_ProviderFails_ReturnsFailureAndCachesNothing()
    {
        //Arrange
        await using var services = Build();
        _search.FailWith = "engine down";
        using var scope = services.CreateScope();
        var search = scope.ServiceProvider.GetRequiredService<ISearchServiceAsync>();

        //Act
        var first = await search.SearchAsync(Queries.Term("products"), Token);
        _search.FailWith = null;
        var second = await search.SearchAsync(Queries.Term("products"), Token);

        //Assert
        first.IsFailed.ShouldBeTrue();
        first.Errors.Single().Message.ShouldBe("engine down");
        second.IsSuccess.ShouldBeTrue();
        second.Value.Items.Single().Document.Id.ShouldBe("products-2");
        _search.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task SearchAsync_TokenAlreadyCancelled_ThrowsWithoutSearching()
    {
        //Arrange
        await using var services = Build();
        using var scope = services.CreateScope();
        var search = scope.ServiceProvider.GetRequiredService<ISearchServiceAsync>();

        //Act
        var act = () => search.SearchAsync(Queries.Term("products"), new CancellationToken(true));

        //Assert
        await Should.ThrowAsync<OperationCanceledException>(act);
        _search.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task SearchAsync_CancelledWhileSearching_ThrowsAndCachesNothing()
    {
        //Arrange
        await using var services = Build();
        using var cancellation = new CancellationTokenSource();
        _search.During = async token =>
        {
            await cancellation.CancelAsync();
            token.ThrowIfCancellationRequested();
        };
        using var scope = services.CreateScope();
        var search = scope.ServiceProvider.GetRequiredService<ISearchServiceAsync>();

        //Act
        var act = () => search.SearchAsync(Queries.Term("products"), cancellation.Token);
        await Should.ThrowAsync<OperationCanceledException>(act);
        _search.During = null;
        var next = await SearchAsync(services, Queries.Term("products"));

        //Assert
        _search.Calls.ShouldBe(2);
        next.Items.Single().Document.Id.ShouldBe("products-2");
    }

    [Fact]
    public async Task SearchAsync_CacheHit_NotifiesObservers()
    {
        //Arrange
        await using var services = Build();
        await SearchAsync(services, Queries.Term("products"));
        using var scope = services.CreateScope();
        var search = scope.ServiceProvider.GetRequiredService<ISearchServiceAsync>();
        var observer = new RecordingObserver<ISearchModel>();
        using var subscription = search.Subscribe(observer);

        //Act
        (await search.SearchAsync(Queries.Term("products"), Token)).IsSuccess.ShouldBeTrue();

        //Assert
        _search.Calls.ShouldBe(1);
        observer.Values.Select(document => document.Id).ShouldBe(["products-1"]);
    }

    [Fact]
    public async Task SearchAsync_SkipReturnsTrue_CallsProviderEveryTime()
    {
        //Arrange
        await using var services = Build(c => c.AddMemorySearchCache(o => o.Skip = query => query.Page == 2));

        //Act
        await SearchAsync(services, Queries.Term("products", page: 2));
        await SearchAsync(services, Queries.Term("products", page: 2));
        await SearchAsync(services, Queries.Term("products"));
        await SearchAsync(services, Queries.Term("products"));

        //Assert
        _search.Calls.ShouldBe(3);
    }

    [Fact]
    public async Task SearchAsync_IndexDisabled_CachesOtherIndexesOnly()
    {
        //Arrange
        await using var services = Build(c => c.AddMemorySearchCache(o =>
            o.Indexes = new Dictionary<string, SearchCacheIndexOptions> { ["PRODUCTS"] = new() { Enabled = false } }));

        //Act
        await SearchAsync(services, Queries.Term("products"));
        await SearchAsync(services, Queries.Term("products"));
        await SearchAsync(services, Queries.Term("articles"));
        await SearchAsync(services, Queries.Term("articles"));

        //Assert
        _search.Calls.ShouldBe(3);
    }

    [Fact]
    public async Task SearchAsync_CacheDisabledButIndexEnabled_CachesThatIndexOnly()
    {
        //Arrange
        await using var services = Build(c => c.AddMemorySearchCache(o =>
        {
            o.Enabled = false;
            o.Indexes["products"] = new SearchCacheIndexOptions { Enabled = true };
        }));

        //Act
        await SearchAsync(services, Queries.Term("products"));
        await SearchAsync(services, Queries.Term("products"));
        await SearchAsync(services, Queries.Term("articles"));
        await SearchAsync(services, Queries.Term("articles"));

        //Assert
        _search.Calls.ShouldBe(3);
    }

    [Fact]
    public async Task SearchAsync_IndexTagsSet_EntryEvictedByThatTag()
    {
        //Arrange
        await using var services = Build(c => c.AddMemorySearchCache(o =>
            o.Indexes["products"] = new SearchCacheIndexOptions { Tags = ["catalogue"] }));
        await SearchAsync(services, Queries.Term("products"));

        //Act
        await services.GetRequiredService<ISearchResultCache>().RemoveByTagAsync("catalogue", Token);
        await SearchAsync(services, Queries.Term("products"));

        //Assert
        _search.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task SearchAsync_KeyPartDiffers_DoesNotShareEntries()
    {
        //Arrange
        var tenant = "a";
        await using var services = Build(c => c.AddMemorySearchCache(o => o.KeyPart = _ => tenant));
        await SearchAsync(services, Queries.Term("products"));

        //Act
        tenant = "b";
        var other = await SearchAsync(services, Queries.Term("products"));
        tenant = "a";
        var same = await SearchAsync(services, Queries.Term("products"));

        //Assert
        _search.Calls.ShouldBe(2);
        other.Items.Single().Document.Id.ShouldBe("products-2");
        same.Items.Single().Document.Id.ShouldBe("products-1");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task SearchAsync_DurationNotPositive_FailsNamingSetting(int seconds)
    {
        //Arrange
        await using var services = Build(c => c.AddMemorySearchCache(o => o.Duration = TimeSpan.FromSeconds(seconds)));
        using var scope = services.CreateScope();

        //Act
        var result = await scope.ServiceProvider.GetRequiredService<ISearchServiceAsync>().SearchAsync(Queries.Term("products"), Token);

        //Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("SearchCacheOptions.Duration");
        _search.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task SearchAsync_CacheThrowsBeforeSearching_SearchesWithoutCache()
    {
        //Arrange
        await using var services = Build(c => c.AddSearchCache<ThrowingCache>());

        //Act
        var result = await SearchAsync(services, Queries.Term("products"));

        //Assert
        _search.Calls.ShouldBe(1);
        result.Items.Single().Document.Id.ShouldBe("products-1");
    }

    [Fact]
    public async Task SearchAsync_HitHoldsUnsupportedValue_ReturnsLiveResultUncached()
    {
        //Arrange
        var provider = new FixedProvider(new Uri("https://example.com"));
        var services = new ServiceCollection();
        services.AddSingleton<ISearchProviderAsync>(provider);
        services.AddBetterSearch(c => c.AddMemorySearchCache());
        await using var built = services.BuildServiceProvider();

        //Act
        var first = await SearchAsync(built, Queries.Term("products"));
        await SearchAsync(built, Queries.Term("products"));

        //Assert
        provider.Calls.ShouldBe(2);
        first.Items.Single().Document.CustomProperties["link"].Single().ShouldBe(new Uri("https://example.com"));
    }

    [Theory]
    [InlineData("index")]
    [InlineData("index many")]
    public async Task IndexingWrite_ObserverSearchesTheIndex_ReadsPastTheEvictedResult(string write)
    {
        //Arrange
        await using var services = Build();
        await SearchAsync(services, Queries.Term("products"));
        var indexing = Indexing(services);
        var seen = new List<string>();
        // Searches from inside OnNext, as an observer that refreshes a view of the index would.
        using var subscription = indexing.Subscribe(new CallbackObserver<SearchDocument>(_ =>
            seen.Add(SearchAsync(services, Queries.Term("products")).GetAwaiter().GetResult().Items.Single().Document.Id)));

        //Act
        await WriteAsync(indexing, write, "products");

        //Assert
        seen.ShouldBe(["products-2"]);
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_ProviderFails_EvictsAndNotifiesNothing()
    {
        //Arrange
        await using var services = Build();
        await SearchAsync(services, Queries.Term("products"));
        _indexing.FailId = "2";
        var indexing = Indexing(services);
        var seen = new List<string>();
        using var subscription = indexing.Subscribe(new CallbackObserver<SearchDocument>(document => seen.Add(document.Id)));

        //Act
        var result = await indexing.IndexMultipleDocumentsAsync(
            [new SearchDocument { Id = "1", Index = "products" }, new SearchDocument { Id = "2", Index = "products" }], Token);
        var after = await SearchAsync(services, Queries.Term("products"));

        //Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("write failed");
        seen.ShouldBeEmpty();
        after.Items.Single().Document.Id.ShouldBe("products-2");
    }

    [Theory]
    [InlineData("create")]
    [InlineData("delete")]
    public async Task IndexingProvider_CreateOrDeleteIndex_EvictsCachedSearches(string write)
    {
        //Arrange
        await using var services = Build();
        await SearchAsync(services, Queries.Term("products"));
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IIndexingProviderAsync>();

        //Act
        var result = write == "create"
            ? await provider.CreateIndexAsync("products", Token)
            : await provider.DeleteIndexAsync("products", Token);
        var after = await SearchAsync(services, Queries.Term("products"));

        //Assert
        result.Value.ShouldBeTrue();
        after.Items.Single().Document.Id.ShouldBe("products-2");
    }

    [Fact]
    public async Task IndexingProvider_EnsureIndexExists_KeepsCachedSearches()
    {
        //Arrange
        await using var services = Build();
        await SearchAsync(services, Queries.Term("products"));
        using var scope = services.CreateScope();

        //Act
        var result = await scope.ServiceProvider.GetRequiredService<IIndexingProviderAsync>().EnsureIndexExistsAsync("products", Token);
        var after = await SearchAsync(services, Queries.Term("products"));

        //Assert
        result.Value.ShouldBeTrue();
        after.Items.Single().Document.Id.ShouldBe("products-1");
    }

    [Fact]
    public async Task SearchProvider_ResolvedDirectly_CachesToo()
    {
        //Arrange
        await using var services = Build();
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<ISearchProviderAsync>();

        //Act
        var first = await provider.SearchAsync(Queries.Term("products"), Token);
        var second = await provider.SearchAsync(Queries.Term("products"), Token);

        //Assert
        provider.ShouldBeOfType<CachingSearchProviderAsyncDecorator>();
        _search.Calls.ShouldBe(1);
        second.Value.Items.Single().Document.Id.ShouldBe(first.Value.Items.Single().Document.Id);
    }

    [Fact]
    public async Task AddBetterSearch_NoCache_DoesNotDecorateProviders()
    {
        //Arrange
        await using var services = Build(_ => { });
        using var scope = services.CreateScope();

        //Act
        var search = scope.ServiceProvider.GetRequiredService<ISearchProviderAsync>();
        var indexing = scope.ServiceProvider.GetRequiredService<IIndexingProviderAsync>();

        //Assert
        search.ShouldBeSameAs(_search);
        indexing.ShouldBeSameAs(_indexing);
    }

    [Fact]
    public async Task SearchAsync_CacheThrowsAfterSearching_ReturnsTheSearchWithoutSearchingAgain()
    {
        //Arrange
        await using var services = Build(c => c.AddSearchCache<StoreFailingCache>());

        //Act
        var result = await SearchAsync(services, Queries.Term("products"));

        //Assert
        _search.Calls.ShouldBe(1);
        result.Items.Single().Document.Id.ShouldBe("products-1");
        result.Items.Single().Document.CustomProperties["call"].ShouldBe([1L]);
    }

    [Fact]
    public async Task SearchAsync_CacheThrowsAfterProviderFails_ReturnsTheFailure()
    {
        //Arrange
        await using var services = Build(c => c.AddSearchCache<StoreFailingCache>());
        _search.FailWith = "engine down";
        using var scope = services.CreateScope();

        //Act
        var result = await scope.ServiceProvider.GetRequiredService<ISearchServiceAsync>().SearchAsync(Queries.Term("products"), Token);

        //Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("engine down");
        _search.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task SearchAsync_KeyedProvider_CachesThroughTheKeyedService()
    {
        //Arrange
        var services = new ServiceCollection();
        services.AddBetterSearch(c => c.AddProvider<CountingSearchProvider, FakeIndexingProvider>("fake").AddMemorySearchCache());
        await using var built = services.BuildServiceProvider();
        using var scope = built.CreateScope();
        var search = scope.ServiceProvider.GetRequiredKeyedService<ISearchServiceAsync>("fake");
        var indexing = scope.ServiceProvider.GetRequiredKeyedService<IIndexingServiceAsync>("fake");

        //Act
        var first = await search.SearchAsync(Queries.Term("products"), Token);
        var cached = await search.SearchAsync(Queries.Term("products"), Token);
        (await indexing.RemoveDocumentsAsync("products", ["1"], Token)).IsSuccess.ShouldBeTrue();
        var evicted = await search.SearchAsync(Queries.Term("products"), Token);

        //Assert
        first.Value.Items.Single().Document.Id.ShouldBe("products-1");
        cached.Value.Items.Single().Document.Id.ShouldBe("products-1");
        evicted.Value.Items.Single().Document.Id.ShouldBe("products-2");
    }

    [Fact]
    public async Task SearchAsync_TwoKeyedProvidersSameIndex_DoNotShareEntries()
    {
        //Arrange
        var services = new ServiceCollection();
        services.AddBetterSearch(c => c
            .AddProvider<CountingSearchProvider, FakeIndexingProvider>("first")
            .AddProvider<SecondCountingSearchProvider, SecondFakeIndexingProvider>("second")
            .AddMemorySearchCache());
        await using var built = services.BuildServiceProvider();
        using var scope = built.CreateScope();
        var second = (CountingSearchProvider)((CachingSearchProviderAsyncDecorator)scope.ServiceProvider.GetRequiredKeyedService<ISearchProviderAsync>("second")).Inner;

        //Act
        (await scope.ServiceProvider.GetRequiredKeyedService<ISearchServiceAsync>("first").SearchAsync(Queries.Term("products"), Token)).IsSuccess.ShouldBeTrue();
        var result = await scope.ServiceProvider.GetRequiredKeyedService<ISearchServiceAsync>("second").SearchAsync(Queries.Term("products"), Token);

        //Assert
        result.IsSuccess.ShouldBeTrue();
        second.Calls.ShouldBe(1);
    }

    private sealed class CallbackObserver<T>(Action<T> onNext) : IObserver<T>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(T value) => onNext(value);
    }

    /// <summary>Runs the factory, then fails to store what it returned.</summary>
    private sealed class StoreFailingCache : ISearchResultCache
    {
        public async ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory,
            SearchCacheEntryOptions options, CancellationToken cancellationToken = default)
        {
            await factory(cancellationToken);
            throw new InvalidOperationException("store full");
        }

        public ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class FixedProvider(object value) : ISearchProviderAsync
    {
        public int Calls { get; private set; }

        public Task<FluentResults.Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default)
        {
            Calls++;
            ISearchResult result = new BaseSearchResults
            {
                Items = [new SearchHit(new BaseSearchModel { Id = "1", CustomProperties = { ["link"] = [value] } }, null)],
                Pagination = new Pagination()
            };
            return Task.FromResult(FluentResults.Result.Ok(result));
        }
    }

    private sealed class ThrowingCache : ISearchResultCache
    {
        public ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory,
            SearchCacheEntryOptions options, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("cache down");

        public ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("cache down");
    }
}
