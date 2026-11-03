using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Meilisearch.Tests;

public sealed class MeilisearchIndexingTest(IIndexingServiceAsync serviceAsync, IIndexingProviderAsync indexingProviderAsync)
    : IndexingTestBase(serviceAsync, indexingProviderAsync);
