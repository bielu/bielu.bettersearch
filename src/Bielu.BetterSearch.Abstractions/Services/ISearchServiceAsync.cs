using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

/// <summary>
/// Searches, gets and counts documents. To run code around a search, decorate this service in DI, as the search cache
/// does.
/// </summary>
public interface ISearchServiceAsync
{
    public Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one document by id; see <see cref="ISearchProviderAsync.GetDocumentAsync"/>.
    /// </summary>
    public Task<Result<SearchDocument?>> GetDocumentAsync(string index, string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Fail<SearchDocument?>($"{GetType().Name} does not support GetDocumentAsync."));

    /// <summary>
    /// Counts the documents a query matches; see <see cref="ISearchProviderAsync.CountAsync"/>.
    /// </summary>
    public Task<Result<long>> CountAsync(ISearchQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Fail<long>($"{GetType().Name} does not support CountAsync."));
}
