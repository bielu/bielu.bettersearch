using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.PostgreSql.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.PostgreSql.Tests;

/// <summary>The PostgreSQL provider has no highlighting yet: every highlighting request fails.</summary>
public sealed class PostgreSqlHighlightingTests(ISearchServiceAsync searchService, IIndexingProviderAsync indexingProvider)
    : HighlightingTestBase(searchService, indexingProvider)
{
    protected override string? UnsupportedMessage => PostgreSqlSearchProviderAsync.UnsupportedHighlighting;
}
