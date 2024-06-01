using Bielu.BetterSearch.Abstractions.Services;
using FluentAssertions;

namespace Bielu.BetterSearch.Tests;

public abstract class IndexingTestBase(IIndexingServiceAsync serviceAsync, IIndexingProviderAsync indexingProviderAsync)
{
    [Fact]
    public async Task CanCreateIndexAsyncTest()
    {
        //Arrange
        var index = "test-index-1";

        //Act
        (await indexingProviderAsync.CreateIndexAsync(index)).IsSuccess.Should().Be(true);
    }
    [Fact]
    public async Task CanDeleteIndexAsyncTest()
    {
        //Arrange
        var index = "test-index-1";
        (await indexingProviderAsync.CreateIndexAsync(index)).IsSuccess.Should().Be(true);

        //Act
        (await indexingProviderAsync.DeleteIndexAsync()).IsSuccess.Should().Be(true);
    }
    [Fact]
    public async Task CanCheckIfIndexExistsAsyncTest()
    {
        //Arrange
        var index = "test-index-1";
        (await indexingProviderAsync.CreateIndexAsync(index)).IsSuccess.Should().Be(true);

        //Act
        (await indexingProviderAsync.IndexExistsAsync()).IsSuccess.Should().Be(true);
    }
    [Fact]
    public async Task EnsureIndexWorksWhenIndexExistsAsyncTest()
    {
        //Arrange
        var index = "test-index-1";

        //Act
        (await indexingProviderAsync.CreateIndexAsync(index)).IsSuccess.Should().Be(true);
        (await indexingProviderAsync.EnsureIndexExistsAsync(index)).IsSuccess.Should().Be(true);
    }
    [Fact]
    public async Task EnsureIndexWorksWhenIndexNotExistsAsyncTest()
    {
        //Arrange
        var index = "test-index-1";
        (await indexingProviderAsync.EnsureIndexExistsAsync(index)).IsSuccess.Should().Be(true);
    }
    [Fact]
    public async Task IndexDocumentAsyncTest()
    {
        //Arrange
        var document = new SearchDocument { Id = "1", Type = "TestDocument", Index = "test-index-1", };

        //Act
        (await serviceAsync.IndexDocumentAsync(document)).IsSuccess.Should().Be(true);
    }

}
