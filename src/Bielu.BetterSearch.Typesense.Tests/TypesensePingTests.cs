using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Typesense.Tests;

public sealed class TypesensePingTests(ISearchProviderAsync searchProvider) : PingTestBase(searchProvider);
