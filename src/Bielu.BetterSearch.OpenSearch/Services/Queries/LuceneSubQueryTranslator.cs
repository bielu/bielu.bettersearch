using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using OpenSearch.Client;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services.Queries;

/// <summary>
/// Raw Lucene syntax, run as a <c>query_string</c> query. Field names are passed to OpenSearch as
/// written, so they need the <c>fields.</c> prefix the documents are stored under.
/// </summary>
internal sealed class LuceneSubQueryTranslator : OpenSearchSubQueryTranslator<LuceneSubQuery>
{
    protected override Task<Result<QueryContainer>> TranslateAsync(LuceneSubQuery query, IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators)
    {
        if (string.IsNullOrWhiteSpace(query.LuceneQuery))
        {
            return Task.FromResult(Result.Fail<QueryContainer>($"{nameof(LuceneSubQuery)} needs a LuceneQuery."));
        }

        return Task.FromResult(Result.Ok<QueryContainer>(new QueryStringQuery
        {
            Query = query.LuceneQuery,
            Boost = OpenSearchFieldNames.Boost(query.BoostValue),
            IsVerbatim = true
        }));
    }
}
