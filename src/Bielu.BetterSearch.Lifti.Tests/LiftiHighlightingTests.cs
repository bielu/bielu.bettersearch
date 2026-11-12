using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Lifti.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Lifti.Tests;

/// <summary>Lifti keeps no document text to mark, so every highlight request fails.</summary>
public sealed class LiftiHighlightingTests(ISearchServiceAsync searchService, IIndexingProviderAsync indexingProvider)
    : HighlightingTestBase(searchService, indexingProvider)
{
    protected override string? UnsupportedMessage => LiftiSearchProviderAsync.UnsupportedHighlighting;
}
