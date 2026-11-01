using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public sealed class ElasticsearchIndexingTest(IIndexingServiceAsync serviceAsync, IIndexingProviderAsync indexingProviderAsync)
    : IndexingTestBase(serviceAsync, indexingProviderAsync);
