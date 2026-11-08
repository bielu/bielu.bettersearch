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
    public async Task IndexMultipleDocumentsAsync_RejectedDocument_SendsOnNextOnlyForIndexedDocuments()
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
        observer.Values.ShouldBe([indexed]);
        observer.CompletedCount.ShouldBe(0);
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
        (await service.IndexDocumentAsync(new SearchDocument { Id = "1", Type = "TestDocument", Index = "test-index", }, Token))
            .IsSuccess.ShouldBeTrue();

        //Assert
        indexed.CompletedCount.ShouldBe(1);
        removed.CompletedCount.ShouldBe(1);
        emptied.CompletedCount.ShouldBe(1);
        indexed.Values.ShouldBeEmpty();
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
        public IDisposable? Subscription { get; set; }
        public int Count { get; private set; }

        public void OnNext(SearchDocument value)
        {
            Count++;
            Subscription?.Dispose();
        }

        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }
}
