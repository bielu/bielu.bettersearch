using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FluentResults;
using EsQuery = Elastic.Clients.Elasticsearch.QueryDsl.Query;

namespace Bielu.BetterSearch.ElasticSearch.Services;

/// <summary>
/// Builds an Elasticsearch <c>bool</c> query from clauses and their occurrence. Elasticsearch follows
/// Lucene: alongside a <c>must</c> or <c>filter</c> clause, <c>should</c> clauses only add to the score.
/// </summary>
internal static class ElasticSearchBoolQueryBuilder
{
    public static async Task<Result<EsQuery>> BuildAsync(
        IEnumerable<(Occurance Occurance, ISearchSubQuery Query)> clauses,
        IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators,
        int boostValue = 1)
    {
        var must = new List<EsQuery>();
        var should = new List<EsQuery>();
        var mustNot = new List<EsQuery>();
        var filter = new List<EsQuery>();

        foreach (var (occurance, subQuery) in clauses)
        {
            var translator = subQueryTranslators.FirstOrDefault(t => t.CanTranslate(subQuery));
            if (translator == null)
            {
                return Result.Fail<EsQuery>($"No translator found for subquery type {subQuery.GetType().Name}");
            }

            var translated = await translator.TranslateAsync(subQuery, subQueryTranslators).ConfigureAwait(false);
            if (translated.IsFailed)
            {
                return translated;
            }

            var target = occurance switch
            {
                Occurance.SHOULD => should,
                Occurance.MUSTNOT => mustNot,
                Occurance.FILTER => filter,
                _ => must
            };
            target.Add(translated.Value);
        }

        return Result.Ok<EsQuery>(new BoolQuery
        {
            Must = must.Count > 0 ? must : null,
            Should = should.Count > 0 ? should : null,
            MustNot = mustNot.Count > 0 ? mustNot : null,
            Filter = filter.Count > 0 ? filter : null,
            Boost = ElasticSearchFieldNames.Boost(boostValue)
        });
    }
}
