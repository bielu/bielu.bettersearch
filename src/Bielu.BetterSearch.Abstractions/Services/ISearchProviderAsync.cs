using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

public interface ISearchProviderAsync
{
    /// <summary>Runs <paramref name="query"/>; a cancelled <paramref name="cancellationToken"/> throws <see cref="OperationCanceledException"/>.</summary>
    Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default);
}
