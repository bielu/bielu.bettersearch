using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FluentResults;
using EsQuery = Elastic.Clients.Elasticsearch.QueryDsl.Query;

namespace Bielu.BetterSearch.ElasticSearch.Services.Queries;

/// <summary>
/// Matches any of the terms. Built from <c>term</c> queries rather than a <c>terms</c> query, which has
/// no case-insensitive option, so it matches the same documents as <see cref="TermSubQuery"/> would.
/// </summary>
internal sealed class TermsSubQueryTranslator : ElasticSearchSubQueryTranslator<TermsSubQuery>
{
    protected override Task<Result<EsQuery>> TranslateAsync(TermsSubQuery query, IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators)
    {
        var field = ElasticSearchFieldNames.Require(query.Field, nameof(TermsSubQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<EsQuery>());
        }

        var values = query.Value?.Where(value => value != null).ToList() ?? [];
        if (values.Count == 0)
        {
            return Task.FromResult(Result.Ok<EsQuery>(new MatchNoneQuery()));
        }

        return Task.FromResult(Result.Ok<EsQuery>(new BoolQuery
        {
            Should = values.Select(value => TermSubQueryTranslator.Create(field.Value, value, null)).ToList(),
            MinimumShouldMatch = 1,
            Boost = ElasticSearchFieldNames.Boost(query.BoostValue)
        }));
    }
}
