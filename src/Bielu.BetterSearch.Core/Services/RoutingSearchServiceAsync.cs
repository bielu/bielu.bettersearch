using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// The unkeyed <see cref="ISearchServiceAsync"/> when indexes are routed: runs each search on the keyed search service
/// that <see cref="IndexRoutes"/> picks for the query's index.
/// </summary>
internal sealed class RoutingSearchServiceAsync(IServiceProvider serviceProvider, IndexRoutes routes) : ISearchServiceAsync
{
    public async Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = routes.KeyFor(query?.Index);
        if (key.IsFailed)
        {
            return key.ToResult<ISearchResult>();
        }

        return await Service(key.Value).SearchAsync(query!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs on the keyed search service routed for <paramref name="index"/>.</summary>
    public async Task<Result<SearchDocument?>> GetDocumentAsync(string index, string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = routes.KeyFor(index);
        if (key.IsFailed)
        {
            return key.ToResult<SearchDocument?>();
        }

        return await Service(key.Value).GetDocumentAsync(index, id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs on the keyed search service routed for the query's index.</summary>
    public async Task<Result<long>> CountAsync(ISearchQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = routes.KeyFor(query?.Index);
        if (key.IsFailed)
        {
            return key.ToResult<long>();
        }

        return await Service(key.Value).CountAsync(query!, cancellationToken).ConfigureAwait(false);
    }

    private ISearchServiceAsync Service(string key) => serviceProvider.GetRequiredKeyedService<ISearchServiceAsync>(key);
}
