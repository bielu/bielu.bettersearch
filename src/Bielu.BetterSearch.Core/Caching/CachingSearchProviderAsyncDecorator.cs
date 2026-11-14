using Bielu.BetterSearch.Abstractions.Caching;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.Caching;

/// <summary>
/// Decorates an <see cref="ISearchProviderAsync"/> with an <see cref="ISearchResultCache"/>. A successful search is
/// cached as a <see cref="CachedSearchResult"/> under a key built from the decorated provider's type,
/// <see cref="SearchCacheOptions.KeyPart"/> and the whole query, and tagged with <see cref="SearchCacheOptions.IndexTag"/>.
/// </summary>
/// <remarks>
/// A failed or cancelled search, and one whose hits hold a field value <see cref="CachedValueKind"/> can't carry, is not
/// cached. A cache that throws before the search runs is skipped, and one that throws while storing a successful search
/// is logged, and the search is returned uncached. A cached search returns the same values as the one that filled the
/// cache, read back from the <see cref="CachedSearchResult"/> each time. The search service sits above this decorator,
/// so its observers are notified of a search's hits whether they came from the cache or from the provider.
/// </remarks>
internal sealed class CachingSearchProviderAsyncDecorator(
    ISearchProviderAsync inner,
    ISearchResultCache cache,
    IOptionsMonitor<SearchCacheOptions>? cacheOptions = null,
    ILogger<CachingSearchProviderAsyncDecorator>? logger = null)
    : ISearchProviderAsync
{
    internal ISearchProviderAsync Inner => inner;

    public async Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default)
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

            var key = SearchCacheKey.TryBuild(query, inner.GetType().FullName ?? string.Empty, options.KeyPart?.Invoke(query));
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
}
