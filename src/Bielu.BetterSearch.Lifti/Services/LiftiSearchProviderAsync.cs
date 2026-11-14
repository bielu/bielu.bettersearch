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

    internal const string UnsupportedGetDocument =
        "The Lifti provider does not support GetDocumentAsync: the index keeps only the words of each document, not its fields, so it cannot return them.";

    internal const string UnsupportedFacets =
        "The Lifti provider does not support facets: its index keeps the words of each field, not the field values to count.";

    /// <summary>Succeeds: the indexes live in this process, so there is no engine to reach.</summary>
    public Task<Result> PingAsync(string? index = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Result.Ok());
    }

    public async Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query.HighlightQueries is { Count: > 0 })
        {
            return Result.Fail<ISearchResult>(UnsupportedHighlighting);
        }

        if (query.FacetQueries is { Count: > 0 })
        {
            return Result.Fail<ISearchResult>(UnsupportedFacets);
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

    public Task<Result<SearchDocument?>> GetDocumentAsync(string index, string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Fail<SearchDocument?>(UnsupportedGetDocument));

    /// <summary>Runs the query as <see cref="SearchAsync"/> does and counts every match, which Lifti holds in memory.</summary>
    public async Task<Result<long>> CountAsync(ISearchQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var check = DocumentLookups.CheckCount(query.Index);
        if (check.IsFailed)
        {
            return check.ToResult<long>();
        }

        var indexResult = await manager.GetOrCreateIndexAsync(query.Index).ConfigureAwait(false);
        if (indexResult.IsFailed)
        {
            return Result.Fail<long>(indexResult.Errors);
        }

        var translateMainQuery = await queryTranslateServiceAsync.TranslateMainQueryAsync(query).ConfigureAwait(false);
        if (translateMainQuery.IsFailed)
        {
            return Result.Fail<long>(translateMainQuery.Errors);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Result.Ok((long)indexResult.Value.Search(translateMainQuery.Value).Count);
    }
}
