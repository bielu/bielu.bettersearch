using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Meilisearch.Tests;

public sealed class MeilisearchPingTests(ISearchProviderAsync searchProvider) : PingTestBase(searchProvider);
