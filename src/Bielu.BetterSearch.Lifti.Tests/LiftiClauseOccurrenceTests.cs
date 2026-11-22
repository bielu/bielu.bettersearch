using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Lifti.Tests;

public sealed class LiftiClauseOccurrenceTests(ISearchServiceAsync searchService, IIndexingProviderAsync indexingProvider)
    : ClauseOccurrenceTestBase(searchService, indexingProvider);
