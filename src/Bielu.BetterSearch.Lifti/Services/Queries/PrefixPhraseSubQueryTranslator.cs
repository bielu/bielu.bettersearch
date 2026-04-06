using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Lifti.Querying;
using Lifti.Querying.QueryParts;

namespace Bielu.BetterSearch.Lifti.Services.Queries;

public class PrefixPhraseSubQueryTranslator : ISubQueryTranslator<IQuery>
{
    public bool CanTranslate(ISearchSubQuery query) => query is PrefixPhraseSubQuery;

    public Task<Result<IQuery>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<IQuery>> subQueryTranslators)
    {
        if (query is not PrefixPhraseSubQuery prefixPhraseQuery)
        {
            return Task.FromResult(Result.Fail<IQuery>("Query is not a prefix phrase query"));
        }

        var value = (prefixPhraseQuery.Value?.ToString() ?? string.Empty).ToUpperInvariant();
        var part = new WildcardQueryPart(
            WildcardQueryFragment.CreateText(value),
            WildcardQueryFragment.MultiCharacter);

        return Task.FromResult(Result.Ok<IQuery>(new Query(part)));
    }
}
