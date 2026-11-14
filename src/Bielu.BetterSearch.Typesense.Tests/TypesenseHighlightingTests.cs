using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Bielu.BetterSearch.Typesense.Services;

namespace Bielu.BetterSearch.Typesense.Tests;

/// <summary>The provider's searches are filters with no search text, so every highlighting request fails.</summary>
public sealed class TypesenseHighlightingTests(ISearchServiceAsync searchService, IIndexingProviderAsync indexingProvider)
    : HighlightingTestBase(searchService, indexingProvider)
{
    protected override string? UnsupportedMessage => TypesenseSearchProviderAsync.UnsupportedHighlighting;
}
