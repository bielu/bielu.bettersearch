using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Meilisearch.Tests;

/// <summary>Sorting against a real Meilisearch.</summary>
public sealed class MeilisearchSortingTests(ISearchServiceAsync searchService, IIndexingProviderAsync indexingProvider)
    : SortingTestBase(searchService, indexingProvider)
{
    protected override bool SortsHits => true;
}
