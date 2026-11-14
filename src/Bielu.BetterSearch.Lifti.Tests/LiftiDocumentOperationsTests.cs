using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Lifti.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Lifti.Tests;

/// <summary>Lifti counts, but keeps no stored fields, so a get and an update fail.</summary>
public sealed class LiftiDocumentOperationsTests(
    ISearchServiceAsync searchService,
    IIndexingServiceAsync indexingService,
    IIndexingProviderAsync indexingProvider) : DocumentOperationsTestBase(searchService, indexingService, indexingProvider)
{
    protected override bool StoresFields => false;

    protected override bool CountOnMissingIndexFails => false;

    protected override string UnsupportedGetMessage => LiftiSearchProviderAsync.UnsupportedGetDocument;

    protected override string UnsupportedUpdateMessage => LiftiIndexingProviderAsync.UnsupportedUpdateDocument;
}
