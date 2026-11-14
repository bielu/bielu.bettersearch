using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.PostgreSql.Services.Queries;

internal abstract class PostgreSqlSubQueryTranslator<TSubQuery> : ISubQueryTranslator<PostgreSqlFilter>
    where TSubQuery : ISearchSubQuery
{
    public bool CanTranslate(ISearchSubQuery query) => query is TSubQuery;

    public Task<Result<PostgreSqlFilter>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<PostgreSqlFilter>> subQueryTranslators) =>
        query is TSubQuery subQuery
            ? Task.FromResult(Translate(subQuery))
            : Task.FromResult(Result.Fail<PostgreSqlFilter>($"Query is not a {typeof(TSubQuery).Name}"));

    protected abstract Result<PostgreSqlFilter> Translate(TSubQuery query);

    /// <summary>The query's field, or a failure when it has none. Any name is safe: it is sent as a parameter.</summary>
    protected static Result<string> RequireField(string? field, string queryName) =>
        string.IsNullOrWhiteSpace(field)
            ? Result.Fail<string>($"{queryName} needs a Field.")
            : Result.Ok(field);
}

/// <summary>Each nested query is a clause of the nested filter, with its own occurrence.</summary>
internal sealed class BooleanSubQueryTranslator : ISubQueryTranslator<PostgreSqlFilter>
{
    public bool CanTranslate(ISearchSubQuery query) => query is BoolSearchSubQuery;

    public Task<Result<PostgreSqlFilter>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<PostgreSqlFilter>> subQueryTranslators) =>
        query is BoolSearchSubQuery boolQuery
            ? PostgreSqlBoolFilterBuilder.BuildAsync((boolQuery.NestedQueries ?? []).Select(nested => (nested?.Occurance ?? Occurance.MUST, nested!)), subQueryTranslators)
            : Task.FromResult(Result.Fail<PostgreSqlFilter>($"Query is not a {nameof(BoolSearchSubQuery)}"));
}

internal sealed class LuceneSubQueryTranslator : PostgreSqlSubQueryTranslator<LuceneSubQuery>
{
    protected override Result<PostgreSqlFilter> Translate(LuceneSubQuery query) =>
        Result.Fail<PostgreSqlFilter>(
            $"The PostgreSQL provider does not support {nameof(LuceneSubQuery)}: PostgreSQL has no Lucene query syntax, and the provider does not translate it.");
}
