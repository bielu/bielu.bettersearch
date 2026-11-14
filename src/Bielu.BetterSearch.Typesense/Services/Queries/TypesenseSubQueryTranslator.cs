using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.Typesense.Services.Queries;

internal abstract class TypesenseSubQueryTranslator<TSubQuery> : ISubQueryTranslator<TypesenseFilter>
    where TSubQuery : ISearchSubQuery
{
    public bool CanTranslate(ISearchSubQuery query) => query is TSubQuery;

    public Task<Result<TypesenseFilter>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<TypesenseFilter>> subQueryTranslators) =>
        query is TSubQuery subQuery
            ? Task.FromResult(Translate(subQuery, subQueryTranslators))
            : Task.FromResult(Result.Fail<TypesenseFilter>($"Query is not a {typeof(TSubQuery).Name}"));

    protected abstract Result<TypesenseFilter> Translate(TSubQuery query, IEnumerable<ISubQueryTranslator<TypesenseFilter>> subQueryTranslators);
}

/// <summary>For a query type Typesense filters cannot express: a failed <c>Result</c> rather than a different query.</summary>
internal abstract class UnsupportedSubQueryTranslator<TSubQuery>(string reason) : TypesenseSubQueryTranslator<TSubQuery>
    where TSubQuery : ISearchSubQuery
{
    protected override Result<TypesenseFilter> Translate(TSubQuery query, IEnumerable<ISubQueryTranslator<TypesenseFilter>> subQueryTranslators) =>
        Result.Fail<TypesenseFilter>($"The Typesense provider does not support {typeof(TSubQuery).Name}: {reason}");
}

internal sealed class FuzzySubQueryTranslator()
    : UnsupportedSubQueryTranslator<FuzzyQuery>(
        "the provider runs every query as a filter, and Typesense filters only match exact values; its typo tolerance applies to the search text");

internal sealed class LuceneSubQueryTranslator()
    : UnsupportedSubQueryTranslator<LuceneSubQuery>("Typesense has no Lucene query syntax, and the provider does not translate it yet");

internal sealed class StringRangeSubQueryTranslator()
    : UnsupportedSubQueryTranslator<StringRange>("Typesense filters compare strings only for equality and prefixes, not by order");
