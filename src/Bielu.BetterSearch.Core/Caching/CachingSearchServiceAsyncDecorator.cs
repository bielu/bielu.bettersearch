using Bielu.BetterSearch.Abstractions.Caching;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.Caching;

/// <summary>
/// Decorates an <see cref="ISearchServiceAsync"/> with an <see cref="ISearchResultCache"/>. A successful search is
/// cached as a <see cref="CachedSearchResult"/> under a key built from <paramref name="providerKey"/>,
/// <see cref="SearchCacheOptions.KeyPart"/> and the whole query, and tagged with <see cref="SearchCacheOptions.IndexTag"/>.
/// </summary>
/// <remarks>
/// A failed or cancelled search, and one whose hits hold a field value <see cref="CachedValueKind"/> can't carry, is not
/// cached. A cache that throws before the search runs is skipped, and one that throws while storing a successful search
/// is logged, and the search is returned uncached. A cached search returns the same values as the one that filled the
/// cache, read back from the <see cref="CachedSearchResult"/> each time.
/// <para>
/// The decorator has its own observers: <see cref="Subscribe"/> registers on it, and it notifies them of the hits of
/// every successful search, from the cache or not, following the <see cref="ISearchServiceAsync"/> contract. The
/// decorated service's own observers are not used through it. Disposing the decorator completes its observers only;
/// the container disposes the decorated service.
/// </para>
/// </remarks>
internal sealed class CachingSearchServiceAsyncDecorator(
    ISearchServiceAsync inner,
    string providerKey,
    ISearchResultCache cache,
    IOptionsMonitor<SearchCacheOptions>? cacheOptions = null,
    ILogger<CachingSearchServiceAsyncDecorator>? logger = null)
    : ISearchServiceAsync
{
    private readonly ObserverList<ISearchModel> _searchObservers = new(logger);

    public IDisposable Subscribe(IObserver<ISearchModel> observer) => _searchObservers.Subscribe(observer);

    public async Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default)
    {
        var result = await SearchThroughCacheAsync(query, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            foreach (var hit in result.Value.Items)
            {
                _searchObservers.Notify(hit.Document);
            }
        }

        return result;
    }

    private async Task<Result<ISearchResult>> SearchThroughCacheAsync(ISearchQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query is null)
        {
            return await inner.SearchAsync(query!, cancellationToken).ConfigureAwait(false);
        }

        var plan = PlanCacheEntry(query);
        if (plan.IsFailed)
        {
            return plan.ToResult<ISearchResult>();
        }

        if (plan.Value is not { } entry)
        {
            return await inner.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        }

        var (key, entryOptions) = entry;
        var factoryRan = false;
        CachedSearchResult? searched = null;
        try
        {
            var cached = await cache.GetOrCreateAsync(key, async token =>
            {
                factoryRan = true;
                var result = await inner.SearchAsync(query, token).ConfigureAwait(false);
                if (result.IsFailed)
                {
                    throw new SearchNotCachedException(result);
                }

                searched = CachedSearchResults.From(result) ?? throw new SearchNotCachedException(result);
                return searched;
            }, entryOptions, cancellationToken).ConfigureAwait(false);
            return CachedSearchResults.ToResult(cached);
        }
        catch (Exception e) when (SearchNotCachedException.Find(e) is { } notCached)
        {
            return notCached.Result;
        }
        catch (Exception e) when (e is not OperationCanceledException && searched is not null)
        {
            // The search succeeded and only storing it failed, so return it rather than search again or fail.
            logger?.LogWarning(e, "The search cache failed to store a search of {Index}, so it was not cached.", query.Index);
            return CachedSearchResults.ToResult(searched);
        }
        catch (Exception e) when (e is not OperationCanceledException && !factoryRan)
        {
            // The cache itself failed before searching, so search without it rather than fail the search.
            logger?.LogWarning(e, "The search cache failed, so the search of {Index} skipped it.", query.Index);
            return await inner.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Not cached: reads the document from the decorated service each time.</summary>
    public Task<Result<SearchDocument?>> GetDocumentAsync(string index, string id, CancellationToken cancellationToken = default) =>
        inner.GetDocumentAsync(index, id, cancellationToken);

    /// <summary>Not cached: counts through the decorated service each time.</summary>
    public Task<Result<long>> CountAsync(ISearchQuery query, CancellationToken cancellationToken = default) =>
        inner.CountAsync(query, cancellationToken);

    /// <summary>
    /// The key and entry options of <paramref name="query"/>, <c>null</c> when it skips the cache, or a failed
    /// <see cref="Result"/> naming the setting that is not valid.
    /// </summary>
    private Result<(string Key, SearchCacheEntryOptions Options)?> PlanCacheEntry(ISearchQuery query)
    {
        try
        {
            var options = cacheOptions?.CurrentValue ?? new SearchCacheOptions();
            var indexOptions = options.ForIndex(query.Index);
            if (!(indexOptions?.Enabled ?? options.Enabled) || options.Skip?.Invoke(query) == true)
            {
                return Result.Ok<(string, SearchCacheEntryOptions)?>(null);
            }

            var duration = indexOptions?.Duration ?? options.Duration;
            if (duration <= TimeSpan.Zero)
            {
                return Result.Fail($"The search cache duration for index '{query.Index}' is {duration}; set SearchCacheOptions.Duration, or the index's SearchCacheIndexOptions.Duration, to more than zero.");
            }

            var key = SearchCacheKey.TryBuild(query, providerKey, options.KeyPart?.Invoke(query));
            if (key is null)
            {
                return Result.Ok<(string, SearchCacheEntryOptions)?>(null);
            }

            var tags = new List<string> { SearchCacheOptions.IndexTag(query.Index) };
            tags.AddRange(indexOptions?.Tags ?? []);
            return Result.Ok<(string, SearchCacheEntryOptions)?>((key, new SearchCacheEntryOptions
            {
                Duration = duration,
                Tags = tags.Distinct(StringComparer.Ordinal).ToList()
            }));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail(new Error($"The search cache options failed for index '{query.Index}': {e.Message}").CausedBy(e));
        }
    }

    /// <summary>Ends the stream of this decorator's observers; the decorated service is disposed by its owner.</summary>
    public void Dispose() => _searchObservers.Complete();
}
