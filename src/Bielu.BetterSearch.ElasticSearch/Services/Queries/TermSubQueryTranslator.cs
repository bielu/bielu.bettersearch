using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FluentResults;
using EsQuery = Elastic.Clients.Elasticsearch.QueryDsl.Query;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.ElasticSearch.Services.Queries;

internal sealed class TermSubQueryTranslator : ElasticSearchSubQueryTranslator<TermSubQuery>
{
    protected override Task<Result<EsQuery>> TranslateAsync(TermSubQuery query, IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators)
    {
        var field = ElasticSearchFieldNames.Require(query.Field, nameof(TermSubQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<EsQuery>());
        }

        if (query.Value == null)
        {
            return Task.FromResult(Result.Fail<EsQuery>($"{nameof(TermSubQuery)} needs a Value."));
        }

        return Task.FromResult(Result.Ok(Create(field.Value, query.Value, ElasticSearchFieldNames.Boost(query.BoostValue))));
    }

    /// <summary>
    /// Text is indexed lower-cased by the default analyzer, so string terms match case-insensitively,
    /// as they do in the other providers. A date is sent as the text it is stored as.
    /// </summary>
    internal static EsQuery Create(string field, object value, float? boost) => new TermQuery
    {
        Field = field,
        Value = FieldValue.FromValue(QueryValueText.IsDate(value) ? QueryValueText.From(value) : value),
        CaseInsensitive = value is string ? true : null,
        Boost = boost
    };
}
