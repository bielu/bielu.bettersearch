using System.Globalization;
using Bielu.BetterSearch.Abstractions.Query;
using FluentResults;

namespace Bielu.BetterSearch.Services;

/// <summary>Checks a sub-query's boost against the engines that score by it, which hold a boost as a 32-bit float.</summary>
internal static class Boosts
{
    /// <summary>
    /// A failed result when <see cref="ISearchSubQuery.BoostValue"/> is a positive finite number that a 32-bit float
    /// can't hold: one that would become infinity, which the engine rejects, or 0, which would drop the clause's score.
    /// </summary>
    public static Result Check(ISearchSubQuery query, string engine)
    {
        var boost = query.BoostValue;
        if (boost is not > 0 || !double.IsFinite(boost) || (float)boost is not (0 or float.PositiveInfinity))
        {
            return Result.Ok();
        }

        return Result.Fail(string.Create(CultureInfo.InvariantCulture,
            $"{query.GetType().Name} has a BoostValue {boost} that {engine} can't hold as a 32-bit float; use a boost between {float.Epsilon} and {float.MaxValue}."));
    }
}
