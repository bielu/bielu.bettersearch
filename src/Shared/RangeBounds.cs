using FluentResults;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// Checks a range the same way in every provider: it needs a minimum, a maximum or both, as a range with neither
/// would match every document that has the field, which is almost always a forgotten bound.
/// </summary>
internal static class RangeBounds
{
    public static Result Require(string queryName, object? min, object? max) =>
        min is null && max is null
            ? Result.Fail($"{queryName} has neither MinValue nor MaxValue; set at least one bound.")
            : Result.Ok();

    /// <summary>
    /// A failure when a bound is <c>NaN</c> or an infinity, which no engine compares as a number: Elasticsearch and
    /// OpenSearch reject it, and a Meilisearch filter reads it as text.
    /// </summary>
    public static Result RequireFinite(string queryName, double? min, double? max) =>
        min is { } low && !double.IsFinite(low) || max is { } high && !double.IsFinite(high)
            ? Result.Fail($"{queryName} has a bound that is not a finite number; use a finite MinValue and MaxValue.")
            : Result.Ok();
}
