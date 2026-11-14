using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.PostgreSql.Tests;

public sealed class PostgreSqlSortingTests(ISearchServiceAsync searchService, IIndexingProviderAsync indexingProvider)
    : SortingTestBase(searchService, indexingProvider)
{
    protected override bool SortsHits => true;
}
