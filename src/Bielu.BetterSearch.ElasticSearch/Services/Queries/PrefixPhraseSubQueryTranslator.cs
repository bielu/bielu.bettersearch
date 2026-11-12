using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FluentResults;
using EsQuery = Elastic.Clients.Elasticsearch.QueryDsl.Query;

namespace Bielu.BetterSearch.ElasticSearch.Services.Queries;

/// <summary>The words in order, the last one as a prefix: <c>match_phrase_prefix</c>.</summary>
internal sealed class PrefixPhraseSubQueryTranslator : ElasticSearchSubQueryTranslator<PrefixPhraseSubQuery>
{
    protected override Task<Result<EsQuery>> TranslateAsync(PrefixPhraseSubQuery query, IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators)
    {
        var field = ElasticSearchFieldNames.Require(query.Field, nameof(PrefixPhraseSubQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<EsQuery>());
        }

        var value = QueryValueText.From(query.Value);
        if (string.IsNullOrEmpty(value))
        {
            return Task.FromResult(Result.Fail<EsQuery>($"{nameof(PrefixPhraseSubQuery)} needs a Value."));
        }

        return Task.FromResult(Result.Ok<EsQuery>(new MatchPhrasePrefixQuery
        {
            Field = field.Value,
            Query = value,
            Boost = ElasticSearchFieldNames.Boost(query.BoostValue)
        }));
    }
}
