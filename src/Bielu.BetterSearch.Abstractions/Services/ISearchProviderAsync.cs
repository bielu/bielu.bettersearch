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

    /// <summary>
    /// Checks that the engine answers, using the connection settings of <paramref name="index"/>, or the shared
    /// settings when it is <c>null</c>. Returns a failed <see cref="Result"/> naming the problem when the engine can't
    /// be reached or answers with an error. A provider that does not override this fails, saying it can't be checked.
    /// </summary>
    Task<Result> PingAsync(string? index = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Fail($"{GetType().Name} does not implement PingAsync, so the engine behind it can't be checked."));
}
