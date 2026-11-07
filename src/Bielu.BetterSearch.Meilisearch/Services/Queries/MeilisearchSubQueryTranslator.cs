using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.Meilisearch.Services.Queries;

internal abstract class MeilisearchSubQueryTranslator<TSubQuery> : ISubQueryTranslator<MeilisearchFilter>
    where TSubQuery : ISearchSubQuery
{
    public bool CanTranslate(ISearchSubQuery query) => query is TSubQuery;

    public Task<Result<MeilisearchFilter>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators) =>
        query is TSubQuery subQuery
            ? Task.FromResult(Translate(subQuery, subQueryTranslators))
            : Task.FromResult(Result.Fail<MeilisearchFilter>($"Query is not a {typeof(TSubQuery).Name}"));

    protected abstract Result<MeilisearchFilter> Translate(TSubQuery query, IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators);

    protected static Result<MeilisearchFilter> Filter(string expression) => Result.Ok(new MeilisearchFilter(expression));
}

/// <summary>For a query type Meilisearch filters cannot express: a failed <c>Result</c> rather than a different query.</summary>
internal abstract class UnsupportedSubQueryTranslator<TSubQuery>(string reason) : MeilisearchSubQueryTranslator<TSubQuery>
    where TSubQuery : ISearchSubQuery
{
    protected override Result<MeilisearchFilter> Translate(TSubQuery query, IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators) =>
        Result.Fail<MeilisearchFilter>($"The Meilisearch provider does not support {typeof(TSubQuery).Name}: {reason}");
}
