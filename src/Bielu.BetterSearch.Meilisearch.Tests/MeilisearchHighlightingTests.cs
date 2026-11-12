using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Meilisearch.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Meilisearch.Tests;

/// <summary>Meilisearch can't highlight the provider's filter-only searches, so every request fails.</summary>
public sealed class MeilisearchHighlightingTests(ISearchServiceAsync searchService, IIndexingProviderAsync indexingProvider)
    : HighlightingTestBase(searchService, indexingProvider)
{
    protected override string? UnsupportedMessage => MeilisearchSearchProviderAsync.UnsupportedHighlighting;
}
