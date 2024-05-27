namespace Bielu.BetterSearch.Abstractions.Services.NullImplementations;

public class DummyIndexingProviderAsync : IIndexingProviderAsync
{
    public Task IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task RemoveAllDocumentsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
