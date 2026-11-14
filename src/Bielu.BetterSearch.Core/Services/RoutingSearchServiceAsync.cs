using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// The unkeyed <see cref="ISearchServiceAsync"/> when indexes are routed: runs each search on the keyed search service
/// that <see cref="IndexRoutes"/> picks for the query's index. Its observers hear every hit that keyed service
/// reports for a search run through this service.
/// </summary>
internal sealed class RoutingSearchServiceAsync : ISearchServiceAsync
{
    private readonly IndexRoutes _routes;
    private readonly ObserverList<ISearchModel> _searchObservers;
    private readonly RoutedServices<ISearchServiceAsync, object> _services;

    public RoutingSearchServiceAsync(IServiceProvider serviceProvider, IndexRoutes routes, ILogger<RoutingSearchServiceAsync>? logger = null)
    {
        _routes = routes;
        _searchObservers = new ObserverList<ISearchModel>(logger);
        _services = new RoutedServices<ISearchServiceAsync, object>(serviceProvider, (service, current) =>
            [service.Subscribe(new ForwardingObserver<ISearchModel>(hit =>
            {
                if (current() is not null)
                {
                    _searchObservers.Notify(hit);
                }
            }))]);
    }

    public IDisposable Subscribe(IObserver<ISearchModel> observer) => _searchObservers.Subscribe(observer);

    public async Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = _routes.KeyFor(query?.Index);
        if (key.IsFailed)
        {
            return key.ToResult<ISearchResult>();
        }

        return await _services.RunAsync(key.Value, new object(), service => service.SearchAsync(query!, cancellationToken)).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _services.Dispose();
        _searchObservers.Complete();
    }
}
