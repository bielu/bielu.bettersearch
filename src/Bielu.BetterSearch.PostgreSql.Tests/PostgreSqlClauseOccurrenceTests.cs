using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.PostgreSql.Tests;

public sealed class PostgreSqlClauseOccurrenceTests(ISearchServiceAsync searchService, IIndexingProviderAsync indexingProvider)
    : ClauseOccurrenceTestBase(searchService, indexingProvider);
