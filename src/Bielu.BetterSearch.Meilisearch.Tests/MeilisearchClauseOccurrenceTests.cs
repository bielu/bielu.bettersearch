using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Meilisearch.Tests;

public sealed class MeilisearchClauseOccurrenceTests(ISearchServiceAsync searchService, IIndexingProviderAsync indexingProvider)
    : ClauseOccurrenceTestBase(searchService, indexingProvider);
