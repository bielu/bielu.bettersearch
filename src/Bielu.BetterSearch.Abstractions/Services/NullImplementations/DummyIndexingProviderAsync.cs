using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services.NullImplementations;

/// <summary>
/// The indexing provider <c>AddBetterSearch</c> registers when no provider extension was called.
/// Every method returns a failed <see cref="Result"/> saying so.
/// </summary>
public class DummyIndexingProviderAsync : IIndexingProviderAsync
{
    private const string NoProviderMessage =
        "No indexing provider is registered. Call a provider extension such as AddElasticSearch, AddOpenSearch, AddMeilisearch or AddLiftiSearch inside AddBetterSearch.";

    public Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) => Task.FromResult(Result.Fail(NoProviderMessage));

    public Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> documents, CancellationToken cancellationToken = default) => Task.FromResult(Result.Fail<int>(NoProviderMessage));

    public Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default) => Task.FromResult(Result.Fail(NoProviderMessage));

    public Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids, CancellationToken cancellationToken = default) => Task.FromResult(Result.Fail<int>(NoProviderMessage));

    public Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest index,
        CancellationToken cancellationToken = default) => Task.FromResult(Result.Fail<long?>(NoProviderMessage));

    public Task<Result<bool>> EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default) => Task.FromResult(Result.Fail<bool>(NoProviderMessage));

    public Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default) => Task.FromResult(Result.Fail<bool>(NoProviderMessage));

    public Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default) => Task.FromResult(Result.Fail<bool>(NoProviderMessage));

    public Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default) => Task.FromResult(Result.Fail<bool>(NoProviderMessage));
}
