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

    private sealed class RecordingObserver : IObserver<SearchDocument>
    {
        public List<SearchDocument> Indexed { get; } = [];
        public int Completed { get; private set; }
        public void OnCompleted() => Completed++;
        public void OnError(Exception error) { }
        public void OnNext(SearchDocument value) => Indexed.Add(value);
    }
}
