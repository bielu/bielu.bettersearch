using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Lifti.Tests;

public class QueryTranslationResultsUnitTests(ISearchServiceAsync serviceAsync, IIndexingServiceAsync indexingServiceAsync) : QueryResultBaseTests(serviceAsync, indexingServiceAsync)
{
    /// <summary>Lifti has no query for every document, so it cannot return the complement.</summary>
    [Fact]
    public override async Task SearchAsync_MustNotOnly_ReturnsTheComplement() => await SearchAsync_MustNotOnly_ShouldFailAsync();
}
