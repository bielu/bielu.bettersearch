using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FluentResults;
using EsQuery = Elastic.Clients.Elasticsearch.QueryDsl.Query;

namespace Bielu.BetterSearch.ElasticSearch.Services.Queries;

internal sealed class PrefixSubQueryTranslator : ElasticSearchSubQueryTranslator<PrefixSubQuery>
{
    protected override Task<Result<EsQuery>> TranslateAsync(PrefixSubQuery query, IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators)
    {
        var field = ElasticSearchFieldNames.Require(query.Field, nameof(PrefixSubQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<EsQuery>());
        }

        var value = QueryValueText.From(query.Value);
        if (string.IsNullOrEmpty(value))
        {
            return Task.FromResult(Result.Fail<EsQuery>($"{nameof(PrefixSubQuery)} needs a Value."));
        }

        return Task.FromResult(Result.Ok<EsQuery>(new PrefixQuery
        {
            Field = field.Value,
            Value = value,
            CaseInsensitive = true,
            Boost = ElasticSearchFieldNames.Boost(query.BoostValue)
        }));
    }
}
