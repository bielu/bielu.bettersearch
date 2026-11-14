using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.PostgreSql.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.PostgreSql.Tests;

/// <summary>The PostgreSQL provider has no facets yet: every query with one fails, whatever the facet.</summary>
public sealed class PostgreSqlFacetsTests(ISearchServiceAsync searchService, IIndexingProviderAsync indexingProvider)
    : FacetsTestBase(searchService, indexingProvider)
{
    protected override string UnsupportedFacets => PostgreSqlSearchProviderAsync.UnsupportedFacets;
}
