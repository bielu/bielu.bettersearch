using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using OpenSearch.Client;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services.Queries;

/// <summary>The words in order, the last one as a prefix: <c>match_phrase_prefix</c>.</summary>
internal sealed class PrefixPhraseSubQueryTranslator : OpenSearchSubQueryTranslator<PrefixPhraseSubQuery>
{
    protected override Task<Result<QueryContainer>> TranslateAsync(PrefixPhraseSubQuery query, IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators)
    {
        var field = OpenSearchFieldNames.Require(query.Field, nameof(PrefixPhraseSubQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<QueryContainer>());
        }

        var value = query.Value?.ToString();
        if (string.IsNullOrEmpty(value))
        {
            return Task.FromResult(Result.Fail<QueryContainer>($"{nameof(PrefixPhraseSubQuery)} needs a Value."));
        }

        return Task.FromResult(Result.Ok<QueryContainer>(new MatchPhrasePrefixQuery
        {
            Field = field.Value,
            Query = value,
            Boost = OpenSearchFieldNames.Boost(query.BoostValue),
            IsVerbatim = true
        }));
    }
}
