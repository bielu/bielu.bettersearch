using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using FluentResults;
using N = Bielu.BetterSearch.Typesense.Services.TypesenseFieldNames;

namespace Bielu.BetterSearch.Typesense.Services.Queries;

/// <summary>
/// Compares integers exactly and other numbers as Typesense <c>float</c>s, which hold about seven significant digits.
/// Bounds with a fraction compare integers from the closest whole number inside the range.
/// </summary>
internal sealed class NumericRangeSubQueryTranslator : TypesenseSubQueryTranslator<NumericRange>
{
    protected override Result<TypesenseFilter> Translate(NumericRange query, IEnumerable<ISubQueryTranslator<TypesenseFilter>> subQueryTranslators) =>
        RangeFilter.Numbers(query, nameof(NumericRange), query.MinValue, query.MaxValue);
}

/// <summary>Compares integers exactly, so values beyond 2^53 keep their precision.</summary>
internal sealed class LongRangeSubQueryTranslator : TypesenseSubQueryTranslator<LongRange>
{
    protected override Result<TypesenseFilter> Translate(LongRange query, IEnumerable<ISubQueryTranslator<TypesenseFilter>> subQueryTranslators)
    {
        var field = N.Require(query.Field, nameof(LongRange));
        if (field.IsFailed)
        {
            return field.ToResult<TypesenseFilter>();
        }

        var bounds = RangeBounds.Require(nameof(LongRange), query.MinValue, query.MaxValue);
        if (bounds.IsFailed)
        {
            return bounds.ToResult<TypesenseFilter>();
        }

        var name = N.Name(field.Value);
        return Result.Ok<TypesenseFilter>(new TypesenseFilter.Or([
            RangeFilter.Integers(N.Integers + name, query.MinValue, query.IncludeMinEdge, query.MaxValue, query.IncludeMaxEdge),
            RangeFilter.Floats(N.Numbers + name, query.MinValue, query.IncludeMinEdge, query.MaxValue, query.IncludeMaxEdge)
        ]));
    }
}

/// <summary>Compares the dates the provider stores as Unix milliseconds; an unspecified <see cref="DateTime"/> is UTC.</summary>
internal sealed class DateRangeSubQueryTranslator : TypesenseSubQueryTranslator<DateRangeQuery>
{
    protected override Result<TypesenseFilter> Translate(DateRangeQuery query, IEnumerable<ISubQueryTranslator<TypesenseFilter>> subQueryTranslators)
    {
        var field = N.Require(query.Field, nameof(DateRangeQuery));
        if (field.IsFailed)
        {
            return field.ToResult<TypesenseFilter>();
        }

        var bounds = RangeBounds.Require(nameof(DateRangeQuery), query.MinValue, query.MaxValue);
        return bounds.IsFailed
            ? bounds.ToResult<TypesenseFilter>()
            : Result.Ok(RangeFilter.Integers(N.Dates + N.Name(field.Value),
                TypesenseDocument.ToUnixMilliseconds(query.MinValue?.Instant), query.IncludeMinEdge,
                TypesenseDocument.ToUnixMilliseconds(query.MaxValue?.Instant), query.IncludeMaxEdge));
    }
}

internal static class RangeFilter
{
    private const string NotNegatable =
        "Typesense filters cannot negate a range: a document matches when any of its values lies outside it, not when none lies inside, so a range can't be a MUSTNOT clause.";

    public static Result<TypesenseFilter> Numbers(BaseRangeQuery query, string queryName, double? min, double? max)
    {
        var field = N.Require(query.Field, queryName);
        if (field.IsFailed)
        {
            return field.ToResult<TypesenseFilter>();
        }

        var bounds = RangeBounds.Require(queryName, min, max);
        if (bounds.IsFailed)
        {
            return bounds.ToResult<TypesenseFilter>();
        }

        var finite = RangeBounds.RequireFinite(queryName, min, max);
        if (finite.IsFailed)
        {
            return finite.ToResult<TypesenseFilter>();
        }

        var name = N.Name(field.Value);
        // The integers inside the range: from the first whole number at or above the minimum, to the last at or below the maximum.
        long? intMin = null, intMax = null;
        var intIncludeMin = query.IncludeMinEdge;
        var intIncludeMax = query.IncludeMaxEdge;
        if (min is { } low)
        {
            var ceiling = Math.Ceiling(low);
            if (ceiling >= long.MaxValue)
            {
                return Result.Ok(Floats(N.Numbers + name, min, query.IncludeMinEdge, max, query.IncludeMaxEdge));
            }

            intMin = ceiling <= long.MinValue ? long.MinValue : (long)ceiling;
            intIncludeMin = intIncludeMin || ceiling != low;
        }

        if (max is { } high)
        {
            var floor = Math.Floor(high);
            if (floor <= long.MinValue)
            {
                return Result.Ok(Floats(N.Numbers + name, min, query.IncludeMinEdge, max, query.IncludeMaxEdge));
            }

            intMax = floor >= long.MaxValue ? long.MaxValue : (long)floor;
            intIncludeMax = intIncludeMax || floor != high;
        }

        return Result.Ok<TypesenseFilter>(new TypesenseFilter.Or([
            Integers(N.Integers + name, intMin, intIncludeMin, intMax, intIncludeMax),
            Floats(N.Numbers + name, min, query.IncludeMinEdge, max, query.IncludeMaxEdge)
        ]));
    }

    /// <summary>
    /// Values of an <c>int64</c> field between the bounds present. With both bounds this is one <c>[min..max]</c> range,
    /// so on a field with many values one value must lie inside it, as on Elasticsearch.
    /// </summary>
    public static TypesenseFilter Integers(string field, long? min, bool includeMin, long? max, bool includeMax)
    {
        if (min is { } low && !includeMin)
        {
            if (low == long.MaxValue)
            {
                return TypesenseFilter.MatchNone;
            }

            min = low + 1;
        }

        if (max is { } high && !includeMax)
        {
            if (high == long.MinValue)
            {
                return TypesenseFilter.MatchNone;
            }

            max = high - 1;
        }

        if (min > max)
        {
            return TypesenseFilter.MatchNone;
        }

        return Compare(field, min == null ? null : N.Number(min.Value), max == null ? null : N.Number(max.Value));
    }

    /// <summary>
    /// Values of a <c>float</c> field between the bounds present. Stored values and bounds round to a float the same
    /// way, so an included edge matches the value it names; an excluded edge moves to the next float inward.
    /// </summary>
    public static TypesenseFilter Floats(string field, double? min, bool includeMin, double? max, bool includeMax)
    {
        float? low = min is { } minimum ? (float)minimum : null;
        float? high = max is { } maximum ? (float)maximum : null;
        if (low is { } l && !includeMin)
        {
            low = MathF.BitIncrement(l);
        }

        if (high is { } h && !includeMax)
        {
            high = MathF.BitDecrement(h);
        }

        if (low > high || low is { } infiniteLow && float.IsInfinity(infiniteLow) || high is { } infiniteHigh && float.IsInfinity(infiniteHigh))
        {
            return low is float.NegativeInfinity || high is float.PositiveInfinity
                ? Compare(field, low is float.NegativeInfinity ? null : Float(low), high is float.PositiveInfinity ? null : Float(high))
                : TypesenseFilter.MatchNone;
        }

        return Compare(field, Float(low), Float(high));
    }

    private static string? Float(float? value) => value is { } number ? N.Number((double)number) : null;

    private static TypesenseFilter Compare(string field, string? min, string? max)
    {
        var expression = (min, max) switch
        {
            (not null, not null) => $"{field}:[{min}..{max}]",
            (not null, null) => $"{field}:>={min}",
            (null, not null) => $"{field}:<={max}",
            // Both bounds lay beyond what a float holds: every value of the field is inside.
            _ => $"{field}:>={N.Number((double)float.MinValue)}"
        };
        return new TypesenseFilter.Comparison(field, expression, null, NotNegatable);
    }
}
