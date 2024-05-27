using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Lifti.Extensions;
using Lifti;

namespace Bielu.BetterSearch.Lifti.Services;

public class LiftiIndexingProviderAsync(IClientFactoryAsync<IFullTextIndex<string>> clientFactory) : IIndexingProviderAsync
{
    public async Task IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) => (await clientFactory.GetOrCreateClientAsync(document.Index)).AddAsync(document,cancellationToken);


    public async Task IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public async Task RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public async Task RemoveAllDocumentsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
