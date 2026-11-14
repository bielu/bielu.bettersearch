using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Typesense.Tests;

/// <summary>Sorting against a real Typesense.</summary>
public sealed class TypesenseSortingTests(ISearchServiceAsync searchService, IIndexingProviderAsync indexingProvider)
    : SortingTestBase(searchService, indexingProvider)
{
    protected override bool SortsHits => true;
}
