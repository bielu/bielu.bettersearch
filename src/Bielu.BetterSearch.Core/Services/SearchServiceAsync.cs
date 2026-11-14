using Bielu.BetterSearch.Abstractions.Caching;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Caching;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.Services;

/// <summary>The default <see cref="ISearchServiceAsync"/>; see it for the observer contract.</summary>
/// <remarks>
/// With an <see cref="ISearchResultCache"/>, registered through <c>AddSearchCache</c>, a successful search is cached
/// as a <see cref="CachedSearchResult"/> under a key built from the provider, <see cref="SearchCacheOptions.KeyPart"/>
/// and the whole query, and tagged with <see cref="SearchCacheOptions.IndexTag"/>. A failed or cancelled search, and
/// one whose hits hold a field value <see cref="CachedValueKind"/> can't carry, is not cached. A cached search returns
/// the same values as the one that filled the cache, read back from the <see cref="CachedSearchResult"/> each time, and
/// observers are notified of its hits whether the result came from the cache or from the provider.
/// </remarks>
public class SearchServiceAsync(
    ISearchProviderAsync providerAsync,
    ILogger<SearchServiceAsync>? logger = null,
    ISearchResultCache? cache = null,
    IOptionsMonitor<SearchCacheOptions>? cacheOptions = null)
    : ISearchServiceAsync
{
    private readonly ObserverList<ISearchModel> _searchObservers = new(logger);

    public IDisposable Subscribe(IObserver<ISearchModel> observer) => _searchObservers.Subscribe(observer);

    public async Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await SearchThroughCacheAsync(query, cancellationToken).ConfigureAwait(false);
        if (result.IsFailed)
        {
            return result;
        }

        foreach (var hit in result.Value.Items)
        {
            _searchObservers.Notify(hit.Document);
        }

        return result;
    }

    private async Task<Result<ISearchResult>> SearchThroughCacheAsync(ISearchQuery query, CancellationToken cancellationToken)
    {
        if (cache is null || query is null)
        {
            return await providerAsync.SearchAsync(query!, cancellationToken).ConfigureAwait(false);
        }

        var plan = PlanCacheEntry(query);
        if (plan.IsFailed)
        {
            return plan.ToResult<ISearchResult>();
        }

        if (plan.Value is not { } entry)
        {
            return await providerAsync.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        }

        var (key, entryOptions) = entry;
        var factoryRan = false;
        try
        {
            var cached = await cache.GetOrCreateAsync(key, async token =>
            {
                factoryRan = true;
                var result = await providerAsync.SearchAsync(query, token).ConfigureAwait(false);
                if (result.IsFailed)
                {
                    throw new SearchNotCachedException(result);
                }

                return CachedSearchResults.From(result) ?? throw new SearchNotCachedException(result);
            }, entryOptions, cancellationToken).ConfigureAwait(false);
            return CachedSearchResults.ToResult(cached);
        }
        catch (Exception e) when (SearchNotCachedException.Find(e) is { } notCached)
        {
            return notCached.Result;
        }
        catch (Exception e) when (e is not OperationCanceledException && !factoryRan)
        {
            // The cache itself failed before searching, so search without it rather than fail the search.
            logger?.LogWarning(e, "The search cache failed, so the search of {Index} skipped it.", query.Index);
            return await providerAsync.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        }
    }

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

            var key = SearchCacheKey.TryBuild(query, providerAsync.GetType().FullName ?? string.Empty, options.KeyPart?.Invoke(query));
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

    /// <summary>Ends the stream: each observer gets <see cref="IObserver{T}.OnCompleted"/> once and is unsubscribed.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Ends the stream when <paramref name="disposing"/> is <c>true</c>. Never throws, and later calls do nothing.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _searchObservers.Complete();
        }
    }
}
