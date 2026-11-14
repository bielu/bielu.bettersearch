using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Bielu.BetterSearch.Typesense.Services;

namespace Bielu.BetterSearch.Typesense.Tests;

/// <summary>The Typesense provider has no facets yet: every query with one fails, whatever the facet.</summary>
public sealed class TypesenseFacetsTests(ISearchServiceAsync searchService, IIndexingProviderAsync indexingProvider)
    : FacetsTestBase(searchService, indexingProvider)
{
    protected override string UnsupportedFacets => TypesenseSearchProviderAsync.UnsupportedFacets;
}
