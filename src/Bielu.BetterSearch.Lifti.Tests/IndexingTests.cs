using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Lifti.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Lifti.Tests;

public class IndexingTests(IIndexingServiceAsync serviceAsync, IIndexingProviderAsync providerAsync, ILiftiIndexManager indexManager)
    : IndexingTestBase(serviceAsync, providerAsync)
{
    // Decided in #40: Lifti indexes live in memory, so every index is missing after a restart. Removing a document
    // from one reports success, as the document is already gone, instead of failing like the other providers.
    protected override bool RemovingFromMissingIndexSucceeds => true;

    protected override bool BulkIndexingIsAllOrNothing => true;

    // Lifti keys documents by id, and throws on a document without one.
    protected override SearchDocument CreateDocumentRejectedByEngine(string index) =>
        new() { Id = null!, Type = "TestDocument", Index = index };

    protected override async Task<long> CountDocumentsAsync(string index) =>
        (await indexManager.GetIndexAsync(index)).Value?.Count ?? 0;
}
