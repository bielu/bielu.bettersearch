using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using OpenSearch.Client;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services;

/// <summary>
/// Builds an OpenSearch <c>bool</c> query from clauses and their occurrence. OpenSearch follows
/// Lucene: alongside a <c>must</c> or <c>filter</c> clause, <c>should</c> clauses only add to the score.
/// </summary>
internal static class OpenSearchBoolQueryBuilder
{
    public static async Task<Result<QueryContainer>> BuildAsync(
        IEnumerable<(Occurance Occurance, ISearchSubQuery Query)> clauses,
        IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators,
        int boostValue = 1)
    {
        var must = new List<QueryContainer>();
        var should = new List<QueryContainer>();
        var mustNot = new List<QueryContainer>();
        var filter = new List<QueryContainer>();

        foreach (var (occurance, subQuery) in clauses)
        {
            var translator = subQueryTranslators.FirstOrDefault(t => t.CanTranslate(subQuery));
            if (translator == null)
            {
                return Result.Fail<QueryContainer>($"No translator found for subquery type {subQuery.GetType().Name}");
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

        // Verbatim, so the client sends the bool query even when it has no clauses.
        return Result.Ok<QueryContainer>(new BoolQuery
        {
            Must = must.Count > 0 ? must : null,
            Should = should.Count > 0 ? should : null,
            MustNot = mustNot.Count > 0 ? mustNot : null,
            Filter = filter.Count > 0 ? filter : null,
            Boost = OpenSearchFieldNames.Boost(boostValue),
            IsVerbatim = true
        });
    }
}
