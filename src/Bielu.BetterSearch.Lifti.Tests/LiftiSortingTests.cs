using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Lifti.Tests;

/// <summary>Lifti keeps hits in score order, but rejects a malformed <c>SortOrder</c> as the other providers do.</summary>
public sealed class LiftiSortingTests(ISearchServiceAsync searchService, IIndexingProviderAsync indexingProvider)
    : SortingTestBase(searchService, indexingProvider)
{
    protected override bool SortsHits => false;
}
