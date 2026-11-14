using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public sealed class ElasticsearchPingTests(ISearchProviderAsync searchProvider) : PingTestBase(searchProvider);
