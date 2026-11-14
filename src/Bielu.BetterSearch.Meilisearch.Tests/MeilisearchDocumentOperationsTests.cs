using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Meilisearch.Tests;

/// <summary>Getting, counting and updating documents against a real Meilisearch.</summary>
public sealed class MeilisearchDocumentOperationsTests(
    ISearchServiceAsync searchService,
    IIndexingServiceAsync indexingService,
    IIndexingProviderAsync indexingProvider) : DocumentOperationsTestBase(searchService, indexingService, indexingProvider)
{
    protected override bool StoresFields => true;
}
