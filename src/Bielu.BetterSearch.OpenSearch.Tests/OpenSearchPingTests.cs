using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public sealed class OpenSearchPingTests(ISearchProviderAsync searchProvider) : PingTestBase(searchProvider);
