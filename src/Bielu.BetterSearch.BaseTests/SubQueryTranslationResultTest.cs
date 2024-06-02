using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentAssertions;

namespace Bielu.BetterSearch.Tests;

public abstract class QueryResultBaseTests(ISearchServiceAsync serviceAsync, IIndexingServiceAsync indexingServiceAsync)
{
    private async Task CreateExampleDocuments(string index)
    {
        var documents = new List<SearchDocument>
        {
            new SearchDocument()
            {
                Id = "1",
                Fields = new Dictionary<string, IList<object>>()
                {
                    { "Name", ["Jane Doe"] },
                    { "Age", [25] }
                }
            },
            new SearchDocument()
            {
                Id = "2",
                Fields = new Dictionary<string, IList<object>>()
                {
                    { "Name", ["John Doe 2"] },
                    { "Age", [30] }
                }
            },
            new SearchDocument()
            {
                Id = "3",
                Fields = new Dictionary<string, IList<object>>()
                {
                    { "Name", ["John Doe 3"] },
                    { "Age", [35] }
                }
            }

        };

        foreach (var document in documents)
        {
            await indexingServiceAsync.IndexDocumentAsync(document);
        }
    }
    [Fact]
    public async Task CanSearchWithPrefixQueryAsyncTest()
    {
        var index = "test-index-1";
        await CreateExampleDocuments(index);
        //Arrange
        var query = new SearchQuery();
        query.Index = index;
        query.Add(Occurance.MUST, new PrefixSubQuery()
        {
            Field = "Name",
            Value = "John"
        });
        //Act
        var result = await serviceAsync.SearchAsync(query);

        //Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Items.Should().NotBeEmpty();
        result.Value.Items.Count().Should().BeGreaterThan(0);
    }
}
