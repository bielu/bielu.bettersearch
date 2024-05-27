using Bielu.BetterSearch.Abstractions.Services;
using FluentAssertions;

namespace Bielu.BetterSearch.Tests;

public abstract class IndexingTestBase(IIndexingServiceAsync serviceAsync)
{
    //test to check if single document is index
    [Fact]
    public async Task IndexDocumentAsyncTest()
    {
        //Arrange
        var document = new SearchDocument { Id = "1", Type = "TestDocument", Index = "test-index-1", };

        //Act
        (await serviceAsync.IndexDocumentAsync(document)).IsSuccess.Should().Be(true);
    }

}
