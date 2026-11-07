using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.Meilisearch.Services;

/// <summary>
/// Combines clauses into one filter with Lucene semantics, as Elasticsearch does: every <c>MUST</c> and
/// <c>FILTER</c> clause must match and no <c>MUSTNOT</c> clause may. <c>SHOULD</c> clauses are only required,
/// as at least one match, when there is no <c>MUST</c> or <c>FILTER</c>; beside one they would only add to the
/// score, which filters don't compute. No clauses match every document.
/// </summary>
internal static class MeilisearchBoolFilterBuilder
{
    public static async Task<Result<MeilisearchFilter>> BuildAsync(
        IEnumerable<(Occurance Occurance, ISearchSubQuery Query)> clauses,
        IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators)
    {
        var required = new List<string>();
        var should = new List<string>();
        var mustNot = new List<string>();

        foreach (var (occurance, subQuery) in clauses)
        {
            var translator = subQueryTranslators.FirstOrDefault(t => t.CanTranslate(subQuery));
            if (translator == null)
            {
                return Result.Fail<MeilisearchFilter>($"No translator found for subquery type {subQuery.GetType().Name}");
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
                _ => required
            };
            target.Add(Group(translated.Value.Expression));
        }

        var parts = new List<string>(required);
        if (required.Count == 0 && should.Count > 0)
        {
            parts.Add(should.Count == 1 ? should[0] : Group(string.Join(" OR ", should)));
        }

        parts.AddRange(mustNot.Select(clause => $"NOT {clause}"));
        if (parts.Count == 0 || required.Count == 0 && should.Count == 0)
        {
            // MUSTNOT on its own returns every other document.
            parts.Insert(0, MeilisearchFieldNames.MatchAll);
        }

        return Result.Ok(new MeilisearchFilter(string.Join(" AND ", parts)));
    }

    private static string Group(string expression) => $"({expression})";
}
