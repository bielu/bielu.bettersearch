using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// The unkeyed <see cref="IIndexLifecycleServiceAsync"/> when indexes are routed: runs each call on the keyed lifecycle
/// service that <see cref="IndexRoutes"/> picks for the index being rebuilt (<see cref="IndexRebuild.Index"/> once the
/// rebuild has started), so a rebuild runs on the provider that holds the index.
/// </summary>
internal sealed class RoutingIndexLifecycleServiceAsync(IServiceProvider serviceProvider, IndexRoutes routes) : IIndexLifecycleServiceAsync
{
    public Task<Result<IndexRebuild>> StartRebuildAsync(string index, CancellationToken cancellationToken = default) =>
        RunAsync(index, service => service.StartRebuildAsync(index, cancellationToken));

    public Task<Result<int>> IndexRebuildDocumentsAsync(IndexRebuild rebuild, IEnumerable<SearchDocument> documents,
        CancellationToken cancellationToken = default) =>
        RunAsync(rebuild?.Index, service => service.IndexRebuildDocumentsAsync(rebuild!, documents, cancellationToken));

    public Task<Result<IReadOnlyList<string>>> CompleteRebuildAsync(IndexRebuild rebuild, CancellationToken cancellationToken = default) =>
        RunAsync(rebuild?.Index, service => service.CompleteRebuildAsync(rebuild!, cancellationToken));

    public Task<Result<bool>> AbortRebuildAsync(IndexRebuild rebuild, CancellationToken cancellationToken = default) =>
        RunAsync(rebuild?.Index, service => service.AbortRebuildAsync(rebuild!, cancellationToken));

    private Task<Result<T>> RunAsync<T>(string? index, Func<IIndexLifecycleServiceAsync, Task<Result<T>>> action)
    {
        var key = routes.KeyFor(index);
        return key.IsFailed
            ? Task.FromResult(key.ToResult<T>())
            : action(serviceProvider.GetRequiredKeyedService<IIndexLifecycleServiceAsync>(key.Value));
    }
}
