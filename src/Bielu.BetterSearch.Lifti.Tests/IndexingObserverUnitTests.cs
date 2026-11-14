using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Abstractions.Services.NullImplementations;
using Bielu.BetterSearch.Lifti.Services;
using Bielu.BetterSearch.Services;
using Bielu.BetterSearch.Tests;
using FluentResults;
using Shouldly;

namespace Bielu.BetterSearch.Lifti.Tests;

/// <summary>The observer contract of <see cref="IndexingServiceAsync"/> on the paths the shared base tests can't reach.</summary>
public class IndexingObserverUnitTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IndexingServiceAsync CreateService(IDocumentValidatorAsync validator)
    {
        var manager = new LiftiIndexManager();
        return new IndexingServiceAsync(new LiftiIndexingProviderAsync(new LiftiClientFactoryAsync(manager), manager), validator);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndexDocumentAsync_RejectedDocument_SendsNothingAndKeepsStreamOpen(bool validatorFails)
    {
        //Arrange
        using var service = CreateService(new RejectingValidator(validatorFails));
        var observer = new RecordingObserver<SearchDocument>();
        using var subscription = service.Subscribe(observer);

        //Act
        var result = await service.IndexDocumentAsync(new SearchDocument { Id = "1", Type = "TestDocument", Index = "test-index", }, Token);

        //Assert
        result.IsFailed.ShouldBeTrue();
        observer.Values.ShouldBeEmpty();
        observer.CompletedCount.ShouldBe(0);
        observer.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_RejectedDocument_SendsNothingAndKeepsStreamOpen()
    {
        //Arrange
        var rejected = new SearchDocument { Id = "bad", Type = "TestDocument", Index = "test-index", };
        using var service = CreateService(new RejectingValidator(false, rejected));
        var observer = new RecordingObserver<SearchDocument>();
        using var subscription = service.Subscribe(observer);
        var indexed = new SearchDocument { Id = "1", Type = "TestDocument", Index = "test-index", };

        //Act
        var result = await service.IndexMultipleDocumentsAsync([indexed, rejected], Token);

        //Assert
        result.IsFailed.ShouldBeTrue();
        observer.Values.ShouldBeEmpty();
        observer.CompletedCount.ShouldBe(0);
        (await service.IndexDocumentAsync(indexed, Token)).IsSuccess.ShouldBeTrue();
        observer.Values.ShouldBe([indexed]);
    }

    [Fact]
    public async Task Dispose_Subscribed_SendsOnCompletedOnceToEveryStream()
    {
        //Arrange
        var service = CreateService(new DummyDocumentValidatorAsync());
        var indexed = new RecordingObserver<SearchDocument>();
        var removed = new RecordingObserver<DeleteDocumentRequest>();
        var emptied = new RecordingObserver<DeleteAllDocumentsRequest>();
        service.Subscribe(indexed);
        service.Subscribe(removed);
        service.Subscribe(emptied);

        //Act
        service.Dispose();
        service.Dispose();
        // Operations stay allowed after Dispose, but notify nobody.
        (await service.IndexDocumentAsync(new SearchDocument { Id = "1", Type = "TestDocument", Index = "test-index", }, Token))
            .IsSuccess.ShouldBeTrue();

        //Assert
        indexed.CompletedCount.ShouldBe(1);
        removed.CompletedCount.ShouldBe(1);
        emptied.CompletedCount.ShouldBe(1);
        indexed.Values.ShouldBeEmpty();
    }

    [Fact]
    public async Task Subscribe_AfterDispose_SendsOnCompletedAndNothingElse()
    {
        //Arrange
        var service = CreateService(new DummyDocumentValidatorAsync());
        service.Dispose();
        var indexed = new RecordingObserver<SearchDocument>();
        var removed = new RecordingObserver<DeleteDocumentRequest>();
        var emptied = new RecordingObserver<DeleteAllDocumentsRequest>();

        //Act
        using var indexedSubscription = service.Subscribe(indexed);
        using var removedSubscription = service.Subscribe(removed);
        using var emptiedSubscription = service.Subscribe(emptied);
        (await service.IndexDocumentAsync(new SearchDocument { Id = "1", Type = "TestDocument", Index = "test-index", }, Token))
            .IsSuccess.ShouldBeTrue();
        service.Dispose();

        //Assert
        indexed.CompletedCount.ShouldBe(1);
        removed.CompletedCount.ShouldBe(1);
        emptied.CompletedCount.ShouldBe(1);
        indexed.Values.ShouldBeEmpty();
    }

    [Fact]
    public async Task IndexDocumentAsync_ObserverThrowsInOnNext_ReturnsOkAndNotifiesOtherObservers()
    {
        //Arrange
        using var service = CreateService(new DummyDocumentValidatorAsync());
        var before = new RecordingObserver<SearchDocument>();
        var after = new RecordingObserver<SearchDocument>();
        using var beforeSubscription = service.Subscribe(before);
        using var throwingSubscription = service.Subscribe(new ThrowingObserver<SearchDocument>());
        using var afterSubscription = service.Subscribe(after);
        var first = new SearchDocument { Id = "1", Type = "TestDocument", Index = "test-index", };
        var second = new SearchDocument { Id = "2", Type = "TestDocument", Index = "test-index", };

        //Act
        var single = await service.IndexDocumentAsync(first, Token);
        var batch = await service.IndexMultipleDocumentsAsync([first, second], Token);

        //Assert
        single.IsSuccess.ShouldBeTrue();
        batch.IsSuccess.ShouldBeTrue();
        batch.Value.ShouldBe(2);
        before.Values.ShouldBe([first, first, second]);
        after.Values.ShouldBe([first, first, second]);
    }

    [Fact]
    public async Task RemoveAsync_ObserverThrowsInOnNext_ReturnsOkAndNotifiesOtherObservers()
    {
        //Arrange
        using var service = CreateService(new DummyDocumentValidatorAsync());
        var removed = new RecordingObserver<DeleteDocumentRequest>();
        var emptied = new RecordingObserver<DeleteAllDocumentsRequest>();
        using var s1 = service.Subscribe(new ThrowingObserver<DeleteDocumentRequest>());
        using var s2 = service.Subscribe(removed);
        using var s3 = service.Subscribe(new ThrowingObserver<DeleteAllDocumentsRequest>());
        using var s4 = service.Subscribe(emptied);
        (await service.IndexDocumentAsync(new SearchDocument { Id = "1", Type = "TestDocument", Index = "test-index", }, Token))
            .IsSuccess.ShouldBeTrue();

        //Act
        var removeResult = await service.RemoveDocumentAsync(new DeleteDocumentRequest { Id = "1", Type = "test-index", }, Token);
        var removeAllResult = await service.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = "test-index", }, Token);

        //Assert
        removeResult.IsSuccess.ShouldBeTrue();
        removeAllResult.IsSuccess.ShouldBeTrue();
        removed.Values.Count.ShouldBe(1);
        emptied.Values.Count.ShouldBe(1);
    }

    [Fact]
    public void Dispose_ObserverThrowsInOnCompleted_CompletesEveryOtherObserverWithoutThrowing()
    {
        //Arrange
        var service = CreateService(new DummyDocumentValidatorAsync());
        var indexed = new RecordingObserver<SearchDocument>();
        var removed = new RecordingObserver<DeleteDocumentRequest>();
        var emptied = new RecordingObserver<DeleteAllDocumentsRequest>();
        service.Subscribe(new ThrowingObserver<SearchDocument>());
        service.Subscribe(indexed);
        service.Subscribe(new ThrowingObserver<DeleteDocumentRequest>());
        service.Subscribe(removed);
        service.Subscribe(emptied);

        //Act
        Should.NotThrow(service.Dispose);

        //Assert
        indexed.CompletedCount.ShouldBe(1);
        removed.CompletedCount.ShouldBe(1);
        emptied.CompletedCount.ShouldBe(1);
    }

    [Fact]
    public async Task Subscribe_SameObserverTwice_NotifiesOnceUntilEveryHandleIsDisposed()
    {
        //Arrange
        using var service = CreateService(new DummyDocumentValidatorAsync());
        var observer = new RecordingObserver<SearchDocument>();
        var document = new SearchDocument { Id = "1", Type = "TestDocument", Index = "test-index", };
        var first = service.Subscribe(observer);
        var second = service.Subscribe(observer);

        //Act
        (await service.IndexDocumentAsync(document, Token)).IsSuccess.ShouldBeTrue();
        first.Dispose();
        first.Dispose();
        (await service.IndexDocumentAsync(document, Token)).IsSuccess.ShouldBeTrue();
        second.Dispose();
        (await service.IndexDocumentAsync(document, Token)).IsSuccess.ShouldBeTrue();

        //Assert
        observer.Values.Count.ShouldBe(2);
    }

    [Fact]
    public async Task IndexDocumentAsync_ConcurrentWithObserversUnsubscribingInOnNext_NotifiesWithoutThrowing()
    {
        //Arrange
        using var service = CreateService(new DummyDocumentValidatorAsync());
        var steady = new ConcurrentRecordingObserver();
        using var steadySubscription = service.Subscribe(steady);
        var selfRemoving = Enumerable.Range(0, 50).Select(_ => new SelfUnsubscribingObserver()).ToList();
        foreach (var observer in selfRemoving)
        {
            observer.Subscription = service.Subscribe(observer);
        }

        var documents = Enumerable.Range(0, 200)
            .Select(i => new SearchDocument { Id = i.ToString(System.Globalization.CultureInfo.InvariantCulture), Type = "TestDocument", Index = "test-index", })
            .ToList();

        //Act
        var results = await Task.WhenAll(documents.Select(d => Task.Run(() => service.IndexDocumentAsync(d, Token), Token)));

        //Assert
        results.ShouldAllBe(r => r.IsSuccess);
        steady.Count.ShouldBe(documents.Count);
        selfRemoving.ShouldAllBe(o => o.Count >= 1);
    }

    private sealed class ThrowingObserver<T> : IObserver<T>
    {
        public void OnNext(T value) => throw new InvalidOperationException("OnNext failed");
        public void OnCompleted() => throw new InvalidOperationException("OnCompleted failed");
        public void OnError(Exception error) { }
    }

    private sealed class ConcurrentRecordingObserver : IObserver<SearchDocument>
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public void OnNext(SearchDocument value) => Interlocked.Increment(ref _count);
        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }

    [Fact]
    public async Task IndexDocumentAsync_ObserverUnsubscribesInOnNext_OtherObserversStillNotified()
    {
        //Arrange
        using var service = CreateService(new DummyDocumentValidatorAsync());
        var other = new RecordingObserver<SearchDocument>();
        var selfRemoving = new SelfUnsubscribingObserver();
        selfRemoving.Subscription = service.Subscribe(selfRemoving);
        using var subscription = service.Subscribe(other);
        var document = new SearchDocument { Id = "1", Type = "TestDocument", Index = "test-index", };

        //Act
        (await service.IndexDocumentAsync(document, Token)).IsSuccess.ShouldBeTrue();
        (await service.IndexDocumentAsync(document, Token)).IsSuccess.ShouldBeTrue();

        //Assert
        selfRemoving.Count.ShouldBe(1);
        other.Values.Count.ShouldBe(2);
    }

    [Fact]
    public async Task IndexDocumentAsync_ObserverDisposesServiceInOnNext_LaterObserversGetNoOnNextAfterOnCompleted()
    {
        //Arrange
        var service = CreateService(new DummyDocumentValidatorAsync());
        var disposing = new DisposingObserver(service);
        var later = new RecordingObserver<SearchDocument>();
        service.Subscribe(disposing);
        service.Subscribe(later);

        //Act
        (await service.IndexDocumentAsync(new SearchDocument { Id = "1", Type = "TestDocument", Index = "test-index", }, Token))
            .IsSuccess.ShouldBeTrue();

        //Assert
        disposing.Count.ShouldBe(1);
        later.CompletedCount.ShouldBe(1);
        later.Values.ShouldBeEmpty();
    }

    private sealed class DisposingObserver(IDisposable service) : IObserver<SearchDocument>
    {
        public int Count { get; private set; }

        public void OnNext(SearchDocument value)
        {
            Count++;
            service.Dispose();
        }

        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }

    private sealed class RejectingValidator(bool fail, SearchDocument? only = null) : IDocumentValidatorAsync
    {
        public Task<Result<bool>> ValidateDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default)
        {
            if (only != null && document != only)
            {
                return Task.FromResult<Result<bool>>(true);
            }

            return Task.FromResult(fail ? Result.Fail<bool>("validator failed") : Result.Ok(false));
        }
    }

    private sealed class SelfUnsubscribingObserver : IObserver<SearchDocument>
    {
        private int _count;
        public IDisposable? Subscription { get; set; }
        public int Count => Volatile.Read(ref _count);

        public void OnNext(SearchDocument value)
        {
            Interlocked.Increment(ref _count);
            Subscription?.Dispose();
        }

        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }
}
