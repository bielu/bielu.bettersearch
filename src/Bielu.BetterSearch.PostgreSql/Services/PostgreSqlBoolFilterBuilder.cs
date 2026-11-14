using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.PostgreSql.Services;

/// <summary>
/// Combines clauses into one filter with Lucene semantics, as Elasticsearch does: every <c>MUST</c> and
/// <c>FILTER</c> clause must match and no <c>MUSTNOT</c> clause may. <c>SHOULD</c> clauses are only required,
/// as at least one match, when there is no <c>MUST</c> or <c>FILTER</c>; beside one they would only add to the
/// score, which the provider does not compute. No clauses match every document.
/// </summary>
internal static class PostgreSqlBoolFilterBuilder
{
    public static async Task<Result<PostgreSqlFilter>> BuildAsync(
        IEnumerable<(Occurance Occurance, ISearchSubQuery Query)> clauses,
        IEnumerable<ISubQueryTranslator<PostgreSqlFilter>> subQueryTranslators)
    {
        var required = new List<PostgreSqlFilter>();
        var should = new List<PostgreSqlFilter>();
        var mustNot = new List<PostgreSqlFilter>();
        var successes = new List<ISuccess>();

        foreach (var (occurance, subQuery) in clauses)
        {
            if (subQuery == null)
            {
                return Result.Fail<PostgreSqlFilter>("The query holds a null sub query.");
            }

            var translator = subQueryTranslators.FirstOrDefault(t => t.CanTranslate(subQuery));
            if (translator == null)
            {
                return Result.Fail<PostgreSqlFilter>($"No translator found for subquery type {subQuery.GetType().Name}");
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
            target.Add(translated.Value);
            successes.AddRange(translated.Successes);
        }

        return Result.Ok(Combine(required, should, mustNot)).WithSuccesses(successes);
    }

    /// <summary>Joins translated clauses with the semantics above.</summary>
    public static PostgreSqlFilter Combine(IReadOnlyList<PostgreSqlFilter> required, IReadOnlyList<PostgreSqlFilter> should, IReadOnlyList<PostgreSqlFilter> mustNot)
    {
        var parts = new List<PostgreSqlFilter>(required);
        if (required.Count == 0 && should.Count > 0)
        {
            parts.Add(should.Count == 1 ? should[0] : new PostgreSqlFilter.Or(should));
        }

        // MUSTNOT on its own returns every other document: an AND of only negations starts from every document.
        parts.AddRange(mustNot.Select(clause => new PostgreSqlFilter.Not(clause)));
        return parts.Count switch
        {
            0 => PostgreSqlFilter.MatchAll,
            1 => parts[0],
            _ => new PostgreSqlFilter.And(parts)
        };
    }
}
