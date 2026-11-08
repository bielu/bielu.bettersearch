using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using Bielu.BetterSearch.Tests;
using FluentResults;
using Shouldly;

namespace Bielu.BetterSearch.Lifti.Tests;

/// <summary>The observer contract of <see cref="SearchServiceAsync"/>, which matches <see cref="IndexingServiceAsync"/>.</summary>
public class SearchObserverUnitTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly ISearchModel First = new BaseSearchModel { Id = "1" };
    private static readonly ISearchModel Second = new BaseSearchModel { Id = "2" };

    [Fact]
    public async Task SearchAsync_Subscribed_SendsOnNextForEachHitAndKeepsStreamOpen()
    {
        //Arrange
        using var service = new SearchServiceAsync(new FixedSearchProvider(First, Second));
        var observer = new RecordingObserver<ISearchModel>();
        using var subscription = service.Subscribe(observer);

        //Act
        (await service.SearchAsync(new SearchQuery(), Token)).IsSuccess.ShouldBeTrue();

        //Assert
        observer.Values.ShouldBe([First, Second]);
        observer.CompletedCount.ShouldBe(0);
        observer.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ProviderFails_SendsNothing()
    {
        //Arrange
        using var service = new SearchServiceAsync(new FixedSearchProvider(null));
        var observer = new RecordingObserver<ISearchModel>();
        using var subscription = service.Subscribe(observer);

        //Act
        var result = await service.SearchAsync(new SearchQuery(), Token);

        //Assert
        result.IsFailed.ShouldBeTrue();
        observer.Values.ShouldBeEmpty();
        observer.CompletedCount.ShouldBe(0);
    }

    [Fact]
    public async Task SearchAsync_ObserverUnsubscribesInOnNext_ReturnsResultAndNotifiesOthers()
    {
        //Arrange
        using var service = new SearchServiceAsync(new FixedSearchProvider(First, Second));
        var selfRemoving = new SelfUnsubscribingObserver();
        selfRemoving.Subscription = service.Subscribe(selfRemoving);
        var other = new RecordingObserver<ISearchModel>();
        using var subscription = service.Subscribe(other);

        //Act
        var result = await service.SearchAsync(new SearchQuery(), Token);

        //Assert
        result.IsSuccess.ShouldBeTrue();
        selfRemoving.Count.ShouldBe(1);
        other.Values.ShouldBe([First, Second]);
    }

    [Fact]
    public async Task SearchAsync_ObserverThrowsInOnNext_ReturnsResultAndNotifiesOthers()
    {
        //Arrange
        using var service = new SearchServiceAsync(new FixedSearchProvider(First, Second));
        using var throwing = service.Subscribe(new ThrowingObserver());
        var other = new RecordingObserver<ISearchModel>();
        using var subscription = service.Subscribe(other);

        //Act
        var result = await service.SearchAsync(new SearchQuery(), Token);

        //Assert
        result.IsSuccess.ShouldBeTrue();
        other.Values.ShouldBe([First, Second]);
    }

    [Fact]
    public async Task Dispose_ObserverThrowsInOnCompleted_CompletesOthersOnceAndLaterSearchesNotifyNobody()
    {
        //Arrange
        var service = new SearchServiceAsync(new FixedSearchProvider(First));
        service.Subscribe(new ThrowingObserver());
        var observer = new RecordingObserver<ISearchModel>();
        service.Subscribe(observer);

        //Act
        Should.NotThrow(service.Dispose);
        service.Dispose();
        (await service.SearchAsync(new SearchQuery(), Token)).IsSuccess.ShouldBeTrue();

        //Assert
        observer.CompletedCount.ShouldBe(1);
        observer.Values.ShouldBeEmpty();
    }

    [Fact]
    public async Task Subscribe_AfterDispose_SendsOnCompletedAndNothingElse()
    {
        //Arrange
        var service = new SearchServiceAsync(new FixedSearchProvider(First));
        service.Dispose();
        var observer = new RecordingObserver<ISearchModel>();

        //Act
        using var subscription = service.Subscribe(observer);
        (await service.SearchAsync(new SearchQuery(), Token)).IsSuccess.ShouldBeTrue();

        //Assert
        observer.CompletedCount.ShouldBe(1);
        observer.Values.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ConcurrentWithObserversUnsubscribingInOnNext_NotifiesWithoutThrowing()
    {
        //Arrange
        using var service = new SearchServiceAsync(new FixedSearchProvider(First, Second));
        var steady = new CountingObserver();
        using var steadySubscription = service.Subscribe(steady);
        for (var i = 0; i < 50; i++)
        {
            var observer = new SelfUnsubscribingObserver();
            observer.Subscription = service.Subscribe(observer);
        }

        //Act
        var results = await Task.WhenAll(Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() => service.SearchAsync(new SearchQuery(), Token), Token)));

        //Assert
        results.ShouldAllBe(r => r.IsSuccess);
        steady.Count.ShouldBe(200);
    }

    private sealed class FixedSearchProvider(params ISearchModel[]? documents) : ISearchProviderAsync
    {
        public Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default)
        {
            if (documents == null)
            {
                return Task.FromResult(Result.Fail<ISearchResult>("search failed"));
            }

            ISearchResult result = new BaseSearchResults { Items = documents.Select(d => new SearchHit(d, null)).ToList(), };
            return Task.FromResult(Result.Ok(result));
        }
    }

    private sealed class ThrowingObserver : IObserver<ISearchModel>
    {
        public void OnNext(ISearchModel value) => throw new InvalidOperationException("OnNext failed");
        public void OnCompleted() => throw new InvalidOperationException("OnCompleted failed");
        public void OnError(Exception error) { }
    }

    private sealed class CountingObserver : IObserver<ISearchModel>
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public void OnNext(ISearchModel value) => Interlocked.Increment(ref _count);
        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }

    private sealed class SelfUnsubscribingObserver : IObserver<ISearchModel>
    {
        private int _count;
        public IDisposable? Subscription { get; set; }
        public int Count => Volatile.Read(ref _count);

        public void OnNext(ISearchModel value)
        {
            Interlocked.Increment(ref _count);
            Subscription?.Dispose();
        }

        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }
}
