using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Lifti.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Lifti.Tests;

public class IndexingTests(IIndexingServiceAsync serviceAsync, IIndexingProviderAsync providerAsync, ILiftiIndexManager indexManager)
    : IndexingTestBase(serviceAsync, providerAsync)
{
    protected override bool RemovingFromMissingIndexSucceeds => true;

    protected override async Task<long> CountDocumentsAsync(string index) =>
        (await indexManager.GetIndexAsync(index)).Value?.Count ?? 0;
}
