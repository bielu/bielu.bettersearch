using Bielu.BetterSearch.Abstractions.Models;
using FluentResults;

namespace Bielu.BetterSearch.Caching;

/// <summary>
/// Thrown from a cache factory to carry a result that must not be cached, such as a failed one, past the cache, which
/// caches nothing when its factory throws.
/// </summary>
internal sealed class SearchNotCachedException(Result<ISearchResult> result)
    : Exception("The search result is not cached.")
{
    public Result<ISearchResult> Result { get; } = result;

    /// <summary>The <see cref="SearchNotCachedException"/> in <paramref name="exception"/> or its inner exceptions, if any.</summary>
    public static SearchNotCachedException? Find(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SearchNotCachedException notCached)
            {
                return notCached;
            }
        }

        return null;
    }
}
