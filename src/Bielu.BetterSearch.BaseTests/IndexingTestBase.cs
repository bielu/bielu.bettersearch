using Bielu.BetterSearch.Abstractions.Services;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

public abstract class IndexingTestBase(IIndexingServiceAsync serviceAsync, IIndexingProviderAsync indexingProviderAsync)
{
    [Fact]
    public async Task CanCreateIndexAsyncTest()
    {
        //Arrange
        var index = "test-index-1";

        //Act
        (await indexingProviderAsync.CreateIndexAsync(index)).IsSuccess.ShouldBeTrue();
    }
    [Fact]
    public async Task CanDeleteIndexAsyncTest()
    {
        //Arrange
        var index = "test-index-1";
        (await indexingProviderAsync.CreateIndexAsync(index)).IsSuccess.ShouldBeTrue();

        //Act
        (await indexingProviderAsync.DeleteIndexAsync(index)).IsSuccess.ShouldBeTrue();
    }
    [Fact]
    public async Task CanCheckIfIndexExistsAsyncTest()
    {
        //Arrange
        var index = "test-index-1";
        (await indexingProviderAsync.CreateIndexAsync(index)).IsSuccess.ShouldBeTrue();

        //Act
        (await indexingProviderAsync.IndexExistsAsync(index)).IsSuccess.ShouldBeTrue();
    }
    [Fact]
    public async Task EnsureIndexWorksWhenIndexExistsAsyncTest()
    {
        //Arrange
        var index = "test-index-1";

        //Act
        (await indexingProviderAsync.CreateIndexAsync(index)).IsSuccess.ShouldBeTrue();
        (await indexingProviderAsync.EnsureIndexExistsAsync(index)).IsSuccess.ShouldBeTrue();
    }
    [Fact]
    public async Task EnsureIndexWorksWhenIndexNotExistsAsyncTest()
    {
        //Arrange
        var index = "test-index-1";
        (await indexingProviderAsync.EnsureIndexExistsAsync(index)).IsSuccess.ShouldBeTrue();
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
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("null fields");
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
    public async Task IndexMultipleDocumentsAsync_NullFieldInLaterDocument_IndexesNone()
    {
        var observer = new RecordingObserver();
        using var subscription = serviceAsync.Subscribe(observer);
        var documents = new List<SearchDocument>
        {
            new() { Id = "1", Type = "TestDocument", Index = "test-index-1" },
            new() { Id = "2", Type = "TestDocument", Index = "test-index-1" },
            new()
            {
                Id = "3",
                Type = "TestDocument",
                Index = "test-index-1",
                Fields = new Dictionary<string, IList<object>> { { "Tags", null! } }
            },
        };

        var result = await serviceAsync.IndexMultipleDocumentsAsync(documents);

        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("Document 3");
        observer.Indexed.ShouldBeEmpty();
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
        (await serviceAsync.IndexMultipleDocumentsAsync(documents)).IsSuccess.ShouldBeTrue();
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

    private sealed class RecordingObserver : IObserver<SearchDocument>
    {
        public List<SearchDocument> Indexed { get; } = [];
        public int Completed { get; private set; }
        public void OnCompleted() => Completed++;
        public void OnError(Exception error) { }
        public void OnNext(SearchDocument value) => Indexed.Add(value);
    }
}
