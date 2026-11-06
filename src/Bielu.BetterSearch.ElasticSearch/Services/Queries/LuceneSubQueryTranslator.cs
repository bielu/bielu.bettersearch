using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FluentResults;
using EsQuery = Elastic.Clients.Elasticsearch.QueryDsl.Query;

namespace Bielu.BetterSearch.ElasticSearch.Services.Queries;

/// <summary>
/// Raw Lucene syntax, run as a <c>query_string</c> query. Field names are passed to Elasticsearch as
/// written, so they need the <c>fields.</c> prefix the documents are stored under.
/// </summary>
internal sealed class LuceneSubQueryTranslator : ElasticSearchSubQueryTranslator<LuceneSubQuery>
{
    protected override Task<Result<EsQuery>> TranslateAsync(LuceneSubQuery query, IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators)
    {
        if (string.IsNullOrWhiteSpace(query.LuceneQuery))
        {
            return Task.FromResult(Result.Fail<EsQuery>($"{nameof(LuceneSubQuery)} needs a LuceneQuery."));
        }

        return Task.FromResult(Result.Ok<EsQuery>(new QueryStringQuery
        {
            Query = query.LuceneQuery,
            Boost = ElasticSearchFieldNames.Boost(query.BoostValue)
        }));
    }
}
