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

    /// <summary>Called after documents change, for engines that must refresh before a change is visible.</summary>
    protected virtual Task DocumentsChangedAsync(string index) => Task.CompletedTask;

    [Fact]
    public async Task RemoveDocumentAsync_MissingDocument_ReturnsSuccess()
    {
        //Arrange
        var index = $"remove-{Guid.NewGuid():N}";
        (await indexingProviderAsync.CreateIndexAsync(index)).IsSuccess.ShouldBeTrue();

        //Act
        var result = await serviceAsync.RemoveDocumentAsync(new DeleteDocumentRequest()
        {
            Id = "never-indexed",
            Type = index,
        });

        //Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveDocumentAsync_AlreadyRemovedDocument_ReturnsSuccess()
    {
        //Arrange
        var index = $"remove-{Guid.NewGuid():N}";
        var document = new SearchDocument { Id = "1", Type = "TestDocument", Index = index, };
        (await serviceAsync.IndexDocumentAsync(document)).IsSuccess.ShouldBeTrue();
        var request = new DeleteDocumentRequest() { Id = document.Id, Type = index, };
        (await serviceAsync.RemoveDocumentAsync(request)).IsSuccess.ShouldBeTrue();

        //Act
        var result = await serviceAsync.RemoveDocumentAsync(request);

        //Assert
        result.IsSuccess.ShouldBeTrue();
        // The document must really be gone, so a provider that succeeds without deleting can't pass.
        await DocumentsChangedAsync(index);
        var remaining = await indexingProviderAsync.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = index });
        remaining.IsSuccess.ShouldBeTrue();
        remaining.Value.ShouldBe(0);
    }

    [Fact]
    public async Task RemoveDocumentAsync_MissingIndex_ReturnsFailure()
    {
        //Arrange
        var index = $"remove-{Guid.NewGuid():N}";

        //Act
        var result = await serviceAsync.RemoveDocumentAsync(new DeleteDocumentRequest()
        {
            Id = "1",
            Type = index,
        });

        //Assert
        result.IsFailed.ShouldBeTrue();
    }
}
