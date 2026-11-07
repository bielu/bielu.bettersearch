using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

public interface ISearchProviderAsync
{
    /// <summary>
    /// Runs <paramref name="query"/>. A <paramref name="cancellationToken"/> cancelled before the call throws
    /// <see cref="OperationCanceledException"/> before the query is validated; one cancelled while the engine
    /// searches throws too, unless the engine cannot be interrupted (Lifti runs a started search to the end).
    /// </summary>
    Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default);
}
