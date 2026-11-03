using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public sealed class OpenSearchIndexingTest(IIndexingServiceAsync serviceAsync, IIndexingProviderAsync indexingProviderAsync)
    : IndexingTestBase(serviceAsync, indexingProviderAsync);
