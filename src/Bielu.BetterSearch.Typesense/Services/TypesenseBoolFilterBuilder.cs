using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.Typesense.Services;

/// <summary>
/// Combines clauses into one filter with Lucene semantics, as Elasticsearch does: every <c>Must</c> and
/// <c>Filter</c> clause must match and no <c>MustNot</c> clause may. <c>Should</c> clauses are only required,
/// as at least one match, when there is no <c>Must</c> or <c>Filter</c>; beside one they would only add to the
/// score, which filters don't compute. No clauses match every document.
/// </summary>
internal static class TypesenseBoolFilterBuilder
{
    public static async Task<Result<TypesenseFilter>> BuildAsync(
        IEnumerable<(Occurrence Occurrence, ISearchSubQuery Query)> clauses,
        IEnumerable<ISubQueryTranslator<TypesenseFilter>> subQueryTranslators)
    {
        var required = new List<TypesenseFilter>();
        var should = new List<TypesenseFilter>();
        var mustNot = new List<TypesenseFilter>();
        var successes = new List<ISuccess>();

        foreach (var (occurrence, subQuery) in clauses)
        {
            if (subQuery == null)
            {
                return Result.Fail<TypesenseFilter>("The query holds a null sub query.");
            }

            var translator = subQueryTranslators.FirstOrDefault(t => t.CanTranslate(subQuery));
            if (translator == null)
            {
                return Result.Fail<TypesenseFilter>($"No translator found for subquery type {subQuery.GetType().Name}");
            }

            var translated = await translator.TranslateAsync(subQuery, subQueryTranslators).ConfigureAwait(false);
            if (translated.IsFailed)
            {
                return translated;
            }

            var target = occurrence switch
            {
                Occurrence.Should => should,
                Occurrence.MustNot => mustNot,
                _ => required
            };
            target.Add(translated.Value);
            successes.AddRange(translated.Successes);
        }

        return Result.Ok(Combine(required, should, mustNot)).WithSuccesses(successes);
    }

    /// <summary>Joins translated clauses with the semantics above.</summary>
    public static TypesenseFilter Combine(IReadOnlyList<TypesenseFilter> required, IReadOnlyList<TypesenseFilter> should, IReadOnlyList<TypesenseFilter> mustNot)
    {
        var parts = new List<TypesenseFilter>(required);
        if (required.Count == 0 && should.Count > 0)
        {
            parts.Add(should.Count == 1 ? should[0] : new TypesenseFilter.Or(should));
        }

        // MUSTNOT on its own returns every other document: an AND of only negations starts from every document.
        parts.AddRange(mustNot.Select(clause => new TypesenseFilter.Not(clause)));
        return parts.Count == 1 ? parts[0] : new TypesenseFilter.And(parts);
    }
}
