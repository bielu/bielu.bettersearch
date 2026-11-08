using Bielu.BetterSearch.Abstractions.Services;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

public abstract class IndexingTestBase(IIndexingServiceAsync serviceAsync, IIndexingProviderAsync indexingProviderAsync)
{
    [Fact]
    public async Task CanCreateIndexAsyncTest()
    {
        //Arrange
        var index = UniqueIndex();
        try
        {
            //Act
            (await indexingProviderAsync.CreateIndexAsync(index)).IsSuccess.ShouldBeTrue();

            //Assert
            await IndexShouldExistAsync(index, true);
        }
        finally
        {
            await DeleteIndexQuietlyAsync(index);
        }
    }
    [Fact]
    public async Task CanDeleteIndexAsyncTest()
    {
        //Arrange
        var index = UniqueIndex();
        try
        {
            (await indexingProviderAsync.CreateIndexAsync(index)).IsSuccess.ShouldBeTrue();

            //Act
            (await indexingProviderAsync.DeleteIndexAsync(index)).IsSuccess.ShouldBeTrue();

            //Assert
            await IndexShouldExistAsync(index, false);
        }
        finally
        {
            await DeleteIndexQuietlyAsync(index);
        }
    }
    [Fact]
    public async Task CanCheckIfIndexExistsAsyncTest()
    {
        //Arrange
        var index = UniqueIndex();
        try
        {
            (await indexingProviderAsync.CreateIndexAsync(index)).IsSuccess.ShouldBeTrue();

            //Act and Assert
            await IndexShouldExistAsync(index, true);
        }
        finally
        {
            await DeleteIndexQuietlyAsync(index);
        }
    }
    [Fact]
    public async Task IndexExistsAsync_MissingIndex_ReturnsFalse()
    {
        //Arrange
        var index = UniqueIndex();

        //Act and Assert
        await IndexShouldExistAsync(index, false);
    }
    [Fact]
    public async Task EnsureIndexWorksWhenIndexExistsAsyncTest()
    {
        //Arrange
        var index = UniqueIndex();
        try
        {
            //Act
            (await indexingProviderAsync.CreateIndexAsync(index)).IsSuccess.ShouldBeTrue();
            (await indexingProviderAsync.EnsureIndexExistsAsync(index)).IsSuccess.ShouldBeTrue();

            //Assert
            await IndexShouldExistAsync(index, true);
        }
        finally
        {
            await DeleteIndexQuietlyAsync(index);
        }
    }
    [Fact]
    public async Task EnsureIndexWorksWhenIndexNotExistsAsyncTest()
    {
        //Arrange
        var index = UniqueIndex();
        try
        {
            //Act
            (await indexingProviderAsync.EnsureIndexExistsAsync(index)).IsSuccess.ShouldBeTrue();

            //Assert
            await IndexShouldExistAsync(index, true);
        }
        finally
        {
            await DeleteIndexQuietlyAsync(index);
        }
    }

    /// <summary>An index name no other test uses, so tests sharing an engine can't change what it holds.</summary>
    private static string UniqueIndex() => $"test-index-{Guid.NewGuid():N}";

    /// <summary>Checks <c>IndexExistsAsync</c> succeeds, showing the provider's errors when it does not, and that it reads <paramref name="expected"/>.</summary>
    private async Task IndexShouldExistAsync(string index, bool expected)
    {
        var exists = await indexingProviderAsync.IndexExistsAsync(index);
        exists.IsSuccess.ShouldBeTrue(string.Join("; ", exists.Errors.Select(error => error.Message)));
        exists.Value.ShouldBe(expected);
    }

    /// <summary>Best-effort cleanup, so the shared engines don't keep the index; a failure here must not hide the test's own.</summary>
    private async Task DeleteIndexQuietlyAsync(string index)
    {
        try
        {
            await indexingProviderAsync.DeleteIndexAsync(index);
        }
        catch (Exception)
        {
            // Ignored: the index may never have been created, or the engine may be unreachable.
        }
    }
    [Fact]
    public async Task IndexDocumentAsyncTest()
    {
        //Arrange
        var document = new SearchDocument { Id = "1", Type = "TestDocument", Index = "test-index-1", };

        //Act
        (await serviceAsync.IndexDocumentAsync(document)).IsSuccess.ShouldBeTrue();
    }
    [Fact]
    public async Task IndexDocumentAsync_NullField_Fails()
    {
        var document = new SearchDocument
        {
            Id = "1",
            Type = "TestDocument",
            Index = "test-index-1",
            Fields = new Dictionary<string, IList<object>> { { "Name", ["Jane"] }, { "Tags", null! } }
        };

        var result = await serviceAsync.IndexDocumentAsync(document);

        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("Tags");
    }
    [Fact]
    public async Task IndexDocumentAsync_NullFieldsDictionary_Fails()
    {
        var document = new SearchDocument { Id = "1", Type = "TestDocument", Index = "test-index-1", Fields = null! };

        var result = await serviceAsync.IndexDocumentAsync(document);

        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("empty dictionary");
    }
    [Fact]
    public async Task IndexDocumentAsync_NullValueInField_Fails()
    {
        var document = new SearchDocument
        {
            Id = "1",
            Type = "TestDocument",
            Index = "test-index-1",
            Fields = new Dictionary<string, IList<object>> { { "Name", ["Jane"] }, { "Tags", ["a", null!] } }
        };

        var result = await serviceAsync.IndexDocumentAsync(document);

        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("Tags");
    }
    [Fact]
    public async Task IndexDocumentAsync_NullField_CompletesObservers()
    {
        var observer = new RecordingObserver();
        using var subscription = serviceAsync.Subscribe(observer);
        var document = new SearchDocument
        {
            Id = "1",
            Type = "TestDocument",
            Index = "test-index-1",
            Fields = new Dictionary<string, IList<object>> { { "Tags", null! } }
        };

        (await serviceAsync.IndexDocumentAsync(document)).IsFailed.ShouldBeTrue();

        observer.Indexed.ShouldBeEmpty();
        observer.Completed.ShouldBe(1);
    }
    [Fact]
    public async Task IndexDocumentAsync_ObserverUnsubscribesOnCompleted_ReturnsFailedResult()
    {
        var observer = new UnsubscribingObserver();
        observer.Subscription = serviceAsync.Subscribe(observer);
        using var otherSubscription = serviceAsync.Subscribe(new RecordingObserver());
        var document = new SearchDocument
        {
            Id = "1",
            Type = "TestDocument",
            Index = "test-index-1",
            Fields = new Dictionary<string, IList<object>> { { "Tags", null! } }
        };

        var result = await serviceAsync.IndexDocumentAsync(document);

        result.IsFailed.ShouldBeTrue();
        observer.Completed.ShouldBe(1);
    }
    [Fact]
    public async Task IndexMultipleDocumentsAsync_NullFieldInLaterDocument_IndexesNone()
    {
        var index = $"batch-{Guid.NewGuid():N}";
        (await indexingProviderAsync.CreateIndexAsync(index, Token)).IsSuccess.ShouldBeTrue();
        try
        {
            var observer = new RecordingObserver();
            using var subscription = serviceAsync.Subscribe(observer);
            var documents = new List<SearchDocument>
            {
                new() { Id = "1", Type = "TestDocument", Index = index },
                new() { Id = "2", Type = "TestDocument", Index = index },
                new()
                {
                    Id = "3",
                    Type = "TestDocument",
                    Index = index,
                    Fields = new Dictionary<string, IList<object>> { { "Tags", null! } }
                },
            };

            var result = await serviceAsync.IndexMultipleDocumentsAsync(documents, Token);

            result.IsFailed.ShouldBeTrue();
            result.Errors.ShouldHaveSingleItem().Message.ShouldContain("Document 3");
            observer.Indexed.ShouldBeEmpty();
            observer.Completed.ShouldBe(1);
            // Documents 1 and 2 must not have reached the engine either.
            (await CountDocumentsAsync(index)).ShouldBe(0);
        }
        finally
        {
            await indexingProviderAsync.DeleteIndexAsync(index, Token);
        }
    }
    [Fact]
    public async Task IndexMultipleDocumentsAsync_NullDocument_Fails()
    {
        var documents = new List<SearchDocument> { new() { Id = "1", Type = "TestDocument", Index = "test-index-1" }, null! };

        var result = await serviceAsync.IndexMultipleDocumentsAsync(documents);

        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("document is null");
    }
    [Fact]
    public async Task IndexDocumentsAsyncTest()
    {
        //Arrange
        var documents = new List<SearchDocument>
        {
            new SearchDocument { Id = "1", Type = "TestDocument", Index = "test-index-1", },
            new SearchDocument { Id = "2", Type = "TestDocument", Index = "test-index-1", },
        };

        //Act
        var indexed = await serviceAsync.IndexMultipleDocumentsAsync(documents);

        //Assert
        indexed.IsSuccess.ShouldBeTrue();
        indexed.Value.ShouldBe(documents.Count);
    }
    [Fact]
    public async Task DeleteDocumentAsyncTest()
    {
        //Arrange
        var document = new SearchDocument { Id = "1", Type = "TestDocument", Index = "test-index-1", };
        (await serviceAsync.IndexDocumentAsync(document)).IsSuccess.ShouldBeTrue();

        //Act
        (await serviceAsync.RemoveDocumentAsync(new DeleteDocumentRequest()
        {
            Id = document.Id,
            Type = document.Index,
        })).IsSuccess.ShouldBeTrue();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Counts the documents in <paramref name="index"/>, after whatever refresh the engine needs to see recent changes.</summary>
    protected abstract Task<long> CountDocumentsAsync(string index);

    /// <summary>
    /// Whether removing a document from an index that does not exist succeeds. Lifti keeps indexes in memory, so a
    /// missing index there is expected after a restart and holds no documents; the other engines report a caller mistake.
    /// </summary>
    protected virtual bool RemovingFromMissingIndexSucceeds => false;

    [Fact]
    public async Task RemoveDocumentAsync_MissingDocument_ReturnsSuccess()
    {
        //Arrange
        var index = $"remove-{Guid.NewGuid():N}";
        (await indexingProviderAsync.CreateIndexAsync(index, Token)).IsSuccess.ShouldBeTrue();
        try
        {
            //Act
            var result = await serviceAsync.RemoveDocumentAsync(new DeleteDocumentRequest()
            {
                Id = "never-indexed",
                Type = index,
            }, Token);

            //Assert
            result.IsSuccess.ShouldBeTrue();
        }
        finally
        {
            await indexingProviderAsync.DeleteIndexAsync(index, Token);
        }
    }

    [Fact]
    public async Task RemoveDocumentAsync_AlreadyRemovedDocument_ReturnsSuccess()
    {
        //Arrange
        var index = $"remove-{Guid.NewGuid():N}";
        var document = new SearchDocument { Id = "1", Type = "TestDocument", Index = index, };
        try
        {
            (await serviceAsync.IndexDocumentAsync(document, Token)).IsSuccess.ShouldBeTrue();
            (await CountDocumentsAsync(index)).ShouldBe(1);
            var request = new DeleteDocumentRequest() { Id = document.Id, Type = index, };
            (await serviceAsync.RemoveDocumentAsync(request, Token)).IsSuccess.ShouldBeTrue();

            //Act
            var result = await serviceAsync.RemoveDocumentAsync(request, Token);

            //Assert
            result.IsSuccess.ShouldBeTrue();
            // The document must really be gone, so a provider that succeeds without deleting can't pass.
            (await CountDocumentsAsync(index)).ShouldBe(0);
        }
        finally
        {
            await indexingProviderAsync.DeleteIndexAsync(index, Token);
        }
    }

    [Fact]
    public async Task RemoveDocumentAsync_MissingIndex_ReturnsDocumentedResult()
    {
        //Arrange
        var index = $"remove-{Guid.NewGuid():N}";

        //Act
        var result = await serviceAsync.RemoveDocumentAsync(new DeleteDocumentRequest()
        {
            Id = "1",
            Type = index,
        }, Token);

        //Assert
        result.IsSuccess.ShouldBe(RemovingFromMissingIndexSucceeds);
        (await indexingProviderAsync.IndexExistsAsync(index, Token)).Value.ShouldBeFalse();
    }

    /// <summary>Disposes its subscription when completed, as Rx observers commonly do.</summary>
    private sealed class UnsubscribingObserver : IObserver<SearchDocument>
    {
        public IDisposable? Subscription { get; set; }
        public int Completed { get; private set; }
        public void OnCompleted()
        {
            Completed++;
            Subscription?.Dispose();
        }
        public void OnError(Exception error) { }
        public void OnNext(SearchDocument value) { }
    }

    private sealed class RecordingObserver : IObserver<SearchDocument>
    {
        public List<SearchDocument> Indexed { get; } = [];
        public int Completed { get; private set; }
        public void OnCompleted() => Completed++;
        public void OnError(Exception error) { }
        public void OnNext(SearchDocument value) => Indexed.Add(value);
    }
}
