using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Lifti.Querying;
using Lifti.Querying.QueryParts;

namespace Bielu.BetterSearch.Lifti.Services.Queries;

/// <summary>
/// Combines clauses by their occurrence with Lucene semantics, as Elasticsearch does: every <c>MUST</c>
/// and <c>FILTER</c> clause is required; <c>SHOULD</c> clauses only decide the matches when nothing is
/// required; <c>MUSTNOT</c> clauses exclude. Lifti has no query for every document, so <c>MUSTNOT</c>
/// clauses on their own return a failed <see cref="Result"/> instead of the complement.
/// </summary>
internal static class LiftiBoolQueryBuilder
{
    /// <summary>Returns the combined query part, or <c>null</c> when there are no clauses.</summary>
    public static async Task<Result<IQueryPart?>> BuildAsync(
        IEnumerable<(Occurance Occurance, ISearchSubQuery Query)> clauses,
        IEnumerable<ISubQueryTranslator<IQuery>> subQueryTranslators)
    {
        IQueryPart? required = null;
        IQueryPart? optional = null;
        IQueryPart? excluded = null;

        foreach (var (occurance, subQuery) in clauses)
        {
            var translator = subQueryTranslators.FirstOrDefault(t => t.CanTranslate(subQuery));
            if (translator == null)
            {
                return Result.Fail<IQueryPart?>($"No translator found for subquery type {subQuery.GetType().Name}");
            }

            var translated = await translator.TranslateAsync(subQuery, subQueryTranslators).ConfigureAwait(false);
            if (translated.IsFailed)
            {
                return Result.Fail<IQueryPart?>(translated.Errors);
            }

            var part = translated.Value.Root;
            if (part == null)
            {
                continue;
            }

            switch (occurance)
            {
                case Occurance.SHOULD:
                    optional = optional == null ? part : new OrQueryOperator(optional, part);
                    break;
                case Occurance.MUSTNOT:
                    excluded = excluded == null ? part : new OrQueryOperator(excluded, part);
                    break;
                default:
                    required = required == null ? part : new AndQueryOperator(required, part);
                    break;
            }
        }

        var matches = required ?? optional;
        if (matches == null)
        {
            return excluded == null
                ? Result.Ok<IQueryPart?>(null)
                : Result.Fail<IQueryPart?>("Lifti cannot return every document except the MUSTNOT matches; add a MUST, FILTER or SHOULD clause.");
        }

        return Result.Ok<IQueryPart?>(excluded == null ? matches : new AndNotQueryOperator(matches, excluded));
    }
}
