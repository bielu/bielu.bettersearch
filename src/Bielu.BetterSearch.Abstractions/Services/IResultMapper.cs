using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;

namespace Bielu.BetterSearch.Abstractions.Services;

/// <summary>Maps an engine's search response to an <see cref="ISearchResult"/>.</summary>
/// <typeparam name="T">The engine's search response type.</typeparam>
public interface IResultMapper<T>
{
    /// <summary>
    /// The result of <paramref name="query"/>: its hits, each <see cref="ISearchHit.Document"/> carrying the query's
    /// <c>Index</c>, and its pagination.
    /// </summary>
    ISearchResult MapResult(T searchEngineResult, ISearchQuery query);
}
