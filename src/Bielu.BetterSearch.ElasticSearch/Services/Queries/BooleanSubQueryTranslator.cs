using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using EsQuery = Elastic.Clients.Elasticsearch.QueryDsl.Query;

namespace Bielu.BetterSearch.ElasticSearch.Services.Queries;

/// <summary>Each nested query is a clause of the <c>bool</c> query, with its own occurrence.</summary>
internal sealed class BooleanSubQueryTranslator : ElasticSearchSubQueryTranslator<BoolSearchSubQuery>
{
    protected override Task<Result<EsQuery>> TranslateAsync(BoolSearchSubQuery query, IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators) =>
        ElasticSearchBoolQueryBuilder.BuildAsync(
            query.NestedQueries.Select(nested => (nested.Occurance, nested)),
            subQueryTranslators,
            query.BoostValue);
}
