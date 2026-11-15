using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Typesense.Tests;

public sealed class TypesenseClauseOccurrenceTests(ISearchServiceAsync searchService, IIndexingProviderAsync indexingProvider)
    : ClauseOccurrenceTestBase(searchService, indexingProvider);
