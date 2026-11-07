using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Lifti.Querying;
using Lifti.Querying.QueryParts;

namespace Bielu.BetterSearch.Lifti.Services.Queries;

/// <summary>
/// Combines clauses by their occurrence with Lucene semantics, as Elasticsearch does: every <c>MUST</c>
/// and <c>FILTER</c> clause is required; <c>SHOULD</c> clauses only decide the matches when nothing is
/// required; <c>MUSTNOT</c> clauses exclude. Lifti has no query for every document, so a nested bool
/// with only <c>MUSTNOT</c> clauses hands its exclusions to the parent, and a query with no positive
/// clause anywhere returns a failed <see cref="Result"/> instead of the complement.
/// </summary>
internal static class LiftiBoolQueryBuilder
{
    private const string NoPositiveClause =
        "Lifti cannot return every document except the MUSTNOT matches; add a MUST, FILTER or SHOULD clause.";

    /// <summary>Returns the combined query part, or <c>null</c> when there are no clauses.</summary>
    public static async Task<Result<IQueryPart?>> BuildAsync(
        IEnumerable<(Occurance Occurance, ISearchSubQuery Query)> clauses,
        IEnumerable<ISubQueryTranslator<IQuery>> subQueryTranslators)
    {
        var built = await BuildClausesAsync(clauses, subQueryTranslators).ConfigureAwait(false);
        if (built.IsFailed)
        {
            return built.ToResult<IQueryPart?>();
        }

        var (matches, excluded) = built.Value;
        if (matches == null)
        {
            return excluded == null ? Result.Ok<IQueryPart?>(null) : Result.Fail<IQueryPart?>(NoPositiveClause);
        }

        return Result.Ok<IQueryPart?>(excluded == null ? matches : new AndNotQueryOperator(matches, excluded));
    }

    /// <summary>
    /// Returns the matches and the exclusions separately, so a parent can apply the exclusions of a
    /// nested bool that has nothing to exclude them from.
    /// </summary>
    private static async Task<Result<(IQueryPart? Matches, IQueryPart? Excluded)>> BuildClausesAsync(
        IEnumerable<(Occurance Occurance, ISearchSubQuery Query)> clauses,
        IEnumerable<ISubQueryTranslator<IQuery>> subQueryTranslators)
    {
        IQueryPart? required = null;
        IQueryPart? optional = null;
        IQueryPart? excluded = null;
        var optionalComplement = false;
        // A required clause makes SHOULD optional even when it only excludes and leaves required null.
        var hasRequired = false;
        // Excluding "not x" requires x, but unlike a MUST it leaves SHOULD deciding the matches, as in Lucene.
        IQueryPart? doubleNegated = null;

        foreach (var (occurance, subQuery) in clauses)
        {
            IQueryPart? part;
            if (subQuery is BoolSearchSubQuery nested)
            {
                var nestedClauses = await BuildClausesAsync(
                    nested.NestedQueries.Select(query => (query.Occurance, query)),
                    subQueryTranslators).ConfigureAwait(false);
                if (nestedClauses.IsFailed)
                {
                    return nestedClauses;
                }

                var (nestedMatches, nestedExcluded) = nestedClauses.Value;
                if (nestedMatches == null && nestedExcluded != null)
                {
                    switch (occurance)
                    {
                        // Requiring "not x" excludes x; excluding "not x" requires x.
                        case Occurance.MUST or Occurance.FILTER:
                            excluded = Or(excluded, nestedExcluded);
                            hasRequired = true;
                            continue;
                        case Occurance.MUSTNOT:
                            doubleNegated = And(doubleNegated, nestedExcluded);
                            continue;
                        default:
                            // Only matters when nothing is required, and Lifti can't express "or not x".
                            optionalComplement = true;
                            continue;
                    }
                }

                part = nestedMatches == null || nestedExcluded == null
                    ? nestedMatches
                    : new AndNotQueryOperator(nestedMatches, nestedExcluded);
            }
            else
            {
                var translator = subQueryTranslators.FirstOrDefault(t => t.CanTranslate(subQuery));
                if (translator == null)
                {
                    return Result.Fail<(IQueryPart?, IQueryPart?)>($"No translator found for subquery type {subQuery.GetType().Name}");
                }

                var translated = await translator.TranslateAsync(subQuery, subQueryTranslators).ConfigureAwait(false);
                if (translated.IsFailed)
                {
                    return Result.Fail<(IQueryPart?, IQueryPart?)>(translated.Errors);
                }

                part = translated.Value.Root;
            }

            if (part == null)
            {
                continue;
            }

            switch (occurance)
            {
                case Occurance.SHOULD:
                    optional = Or(optional, part);
                    break;
                case Occurance.MUSTNOT:
                    excluded = Or(excluded, part);
                    break;
                default:
                    required = And(required, part);
                    hasRequired = true;
                    break;
            }
        }

        if (!hasRequired && optionalComplement)
        {
            return Result.Fail<(IQueryPart?, IQueryPart?)>(NoPositiveClause);
        }

        var matches = hasRequired ? required : optional;
        if (doubleNegated != null)
        {
            // A null match is every document, or no positive clause, so the double negation alone is the match.
            matches = And(matches, doubleNegated);
        }

        return Result.Ok<(IQueryPart?, IQueryPart?)>((matches, excluded));
    }

    private static IQueryPart Or(IQueryPart? left, IQueryPart right) => left == null ? right : new OrQueryOperator(left, right);

    private static IQueryPart And(IQueryPart? left, IQueryPart right) => left == null ? right : new AndQueryOperator(left, right);
}
