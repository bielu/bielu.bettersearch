using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// The default <see cref="IIndexLifecycleServiceAsync"/>: runs a rebuild through the indexing provider when it also
/// implements <see cref="IIndexLifecycleProviderAsync"/>, and fails every call when it does not.
/// </summary>
public class IndexLifecycleServiceAsync(IIndexingProviderAsync indexingProvider, IDocumentValidatorAsync documentValidator)
    : IIndexLifecycleServiceAsync
{
    private readonly IIndexLifecycleProviderAsync? _lifecycleProvider = indexingProvider as IIndexLifecycleProviderAsync;

    public Task<Result<IndexRebuild>> StartRebuildAsync(string index, CancellationToken cancellationToken = default)
    {
        if (_lifecycleProvider is null)
        {
            return Task.FromResult(NotSupported<IndexRebuild>());
        }

        return string.IsNullOrWhiteSpace(index)
            ? Task.FromResult(Result.Fail<IndexRebuild>("A rebuild needs an index name."))
            : _lifecycleProvider.StartRebuildAsync(index, cancellationToken);
    }

    public async Task<Result<int>> IndexRebuildDocumentsAsync(IndexRebuild rebuild, IEnumerable<SearchDocument> documents,
        CancellationToken cancellationToken = default)
    {
        if (_lifecycleProvider is null)
        {
            return NotSupported<int>();
        }

        if (rebuild is null)
        {
            return Result.Fail<int>("The rebuild is null.");
        }

        var batch = documents?.ToList() ?? [];
        // Reject the whole batch before indexing any of it, as IndexingServiceAsync does.
        var checkedFields = Result.Merge(batch.Select(IndexingServiceAsync.CheckNullFields).ToArray());
        if (checkedFields.IsFailed)
        {
            return Result.Fail<int>(checkedFields.Errors);
        }

        var otherIndex = batch.FirstOrDefault(document => !string.Equals(document.Index, rebuild.Index, StringComparison.OrdinalIgnoreCase));
        if (otherIndex is not null)
        {
            return Result.Fail<int>($"Document {otherIndex.Id} is for index '{otherIndex.Index}', not '{rebuild.Index}', the index being rebuilt.");
        }

        // Validate every document before sending any, as IndexingServiceAsync does.
        var errors = new List<IError>();
        foreach (var document in batch)
        {
            errors.AddRange((await IndexingServiceAsync.ValidateAsync(documentValidator, document, cancellationToken).ConfigureAwait(false)).Errors);
        }

        if (errors.Count > 0)
        {
            return Result.Fail<int>(errors);
        }

        return batch.Count == 0
            ? Result.Ok(0)
            : await _lifecycleProvider.IndexRebuildDocumentsAsync(rebuild, batch, cancellationToken).ConfigureAwait(false);
    }

    public Task<Result<IReadOnlyList<string>>> CompleteRebuildAsync(IndexRebuild rebuild, CancellationToken cancellationToken = default)
    {
        if (_lifecycleProvider is null)
        {
            return Task.FromResult(NotSupported<IReadOnlyList<string>>());
        }

        return rebuild is null
            ? Task.FromResult(Result.Fail<IReadOnlyList<string>>("The rebuild is null."))
            : _lifecycleProvider.CompleteRebuildAsync(rebuild, cancellationToken);
    }

    public Task<Result<bool>> AbortRebuildAsync(IndexRebuild rebuild, CancellationToken cancellationToken = default)
    {
        if (_lifecycleProvider is null)
        {
            return Task.FromResult(NotSupported<bool>());
        }

        return rebuild is null
            ? Task.FromResult(Result.Fail<bool>("The rebuild is null."))
            : _lifecycleProvider.AbortRebuildAsync(rebuild, cancellationToken);
    }

    private Result<T> NotSupported<T>() => Result.Fail<T>(
        $"{indexingProvider.GetType().Name} does not support index rebuilds: it does not implement {nameof(IIndexLifecycleProviderAsync)}.");
}
