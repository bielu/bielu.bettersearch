using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Typesense.Tests;

/// <summary>Getting, counting and updating documents against a real Typesense.</summary>
public sealed class TypesenseDocumentOperationsTests(
    ISearchServiceAsync searchService,
    IIndexingServiceAsync indexingService,
    IIndexingProviderAsync indexingProvider) : DocumentOperationsTestBase(searchService, indexingService, indexingProvider)
{
    protected override bool StoresFields => true;
}
