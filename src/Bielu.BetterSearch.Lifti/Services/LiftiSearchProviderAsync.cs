using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using FluentResults;
using Lifti;
using Lifti.Querying;

namespace Bielu.BetterSearch.Lifti.Services;

public class LiftiSearchProviderAsync(
    IQueryTranslateServiceAsync<IQuery> queryTranslateServiceAsync,
    ILiftiIndexManager manager,
    IResultMapper<ISearchResults<string>> resultMapper) : ISearchProviderAsync
{
    internal const string UnsupportedHighlighting =
        "The Lifti provider does not support highlighting: the index keeps only the words of each document, not its text, so it cannot mark the matched terms in it.";

    public async Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query.HighlightQueries is { Count: > 0 })
        {
            return Result.Fail<ISearchResult>(UnsupportedHighlighting);
        }

        // Hits stay in score order, but a malformed SortOrder fails as on the other providers.
        var sortOrder = SortOrders.Ordered(query);
        if (sortOrder.IsFailed)
        {
            return sortOrder.ToResult<ISearchResult>();
        }

        var indexResult = await manager.GetOrCreateIndexAsync(query.Index).ConfigureAwait(false);
        if (indexResult.IsFailed)
        {
            return Result.Fail<ISearchResult>(indexResult.Errors);
        }
        var index = indexResult.Value;
        var translateMainQuery = await queryTranslateServiceAsync.TranslateMainQueryAsync(query).ConfigureAwait(false);
        if (translateMainQuery.IsFailed)
        {
            return Result.Fail<ISearchResult>(translateMainQuery.Errors);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var mainResult = index.Search(translateMainQuery.Value);
        var result = resultMapper.MapResult(mainResult);
        return Result.Ok(result);
    }
}
