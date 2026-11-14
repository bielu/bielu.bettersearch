using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Lifti.Tests;

public sealed class LiftiPingTests(ISearchProviderAsync searchProvider) : PingTestBase(searchProvider);
