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
}
