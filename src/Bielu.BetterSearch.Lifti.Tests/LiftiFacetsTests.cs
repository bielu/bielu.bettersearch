using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Lifti.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Lifti.Tests;

/// <summary>Lifti has no facets: every query with one fails, whatever the facet.</summary>
public sealed class LiftiFacetsTests(ISearchServiceAsync searchService, IIndexingProviderAsync indexingProvider)
    : FacetsTestBase(searchService, indexingProvider)
{
    protected override string UnsupportedFacets => LiftiSearchProviderAsync.UnsupportedFacets;
}
