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
    /// Gets the document with <paramref name="id"/> from <paramref name="index"/>, with its fields as search results
    /// return them. <c>null</c> when the engine answers that the document, or the whole index, is not there; a failed
    /// <see cref="Result"/> when the engine could not answer, such as on a 401 or 5xx. A provider that does not
    /// support it returns a failed <see cref="Result"/>, which is also what this default implementation does.
    /// </summary>
    Task<Result<SearchDocument?>> GetDocumentAsync(string index, string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Fail<SearchDocument?>($"{GetType().Name} does not support GetDocumentAsync."));

    /// <summary>
    /// Counts the documents <paramref name="query"/> matches, the same documents <see cref="SearchAsync"/> would
    /// return, without fetching them. <c>Page</c>, <c>PageSize</c>, <c>SortOrder</c>, <c>Properties</c>,
    /// <c>HighlightQueries</c> and <c>FacetQueries</c> are ignored. The count is exact, with no cap at the paging
    /// window. A provider that does not support it returns a failed <see cref="Result"/>, which is also what this
    /// default implementation does.
    /// </summary>
    Task<Result<long>> CountAsync(ISearchQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Fail<long>($"{GetType().Name} does not support CountAsync."));

    /// <summary>
    /// Checks that the engine answers, using the connection settings of <paramref name="index"/>, or the shared
    /// settings when it is <c>null</c>. Returns a failed <see cref="Result"/> naming the problem when the engine can't
    /// be reached or answers with an error. A provider that does not override this fails, saying it can't be checked.
    /// </summary>
    Task<Result> PingAsync(string? index = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Fail($"{GetType().Name} does not implement PingAsync, so the engine behind it can't be checked."));
}
