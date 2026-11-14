using System.Text;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using FluentResults;

namespace Bielu.BetterSearch.Meilisearch.Services.Queries;

internal sealed class NumericRangeSubQueryTranslator : MeilisearchSubQueryTranslator<NumericRange>
{
    protected override Result<MeilisearchFilter> Translate(NumericRange query, IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators) =>
        RangeFilter.Numbers(query, nameof(NumericRange), MeilisearchFieldNames.Fields, query.MinValue, query.MaxValue);
}

/// <summary>Meilisearch stores numbers as doubles, so values beyond 2^53 compare at that precision.</summary>
internal sealed class LongRangeSubQueryTranslator : MeilisearchSubQueryTranslator<LongRange>
{
    protected override Result<MeilisearchFilter> Translate(LongRange query, IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators) =>
        RangeFilter.Numbers(query, nameof(LongRange), MeilisearchFieldNames.Fields, query.MinValue, query.MaxValue);
}

/// <summary>Compares the dates the provider stores as Unix milliseconds; an unspecified <see cref="DateTime"/> is UTC.</summary>
internal sealed class DateRangeSubQueryTranslator : MeilisearchSubQueryTranslator<DateRangeQuery>
{
    protected override Result<MeilisearchFilter> Translate(DateRangeQuery query, IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators) =>
        RangeFilter.Numbers(query, nameof(DateRangeQuery), MeilisearchFieldNames.Dates,
            MeilisearchDocument.ToUnixMilliseconds(query.MinValue?.Instant), MeilisearchDocument.ToUnixMilliseconds(query.MaxValue?.Instant));
}

/// <summary>
/// Compares the field's lower-cased words, as Elasticsearch compares analyzed tokens; a <c>fields.</c> name compares
/// whole values. Meilisearch compares strings ignoring case either way, so the bounds are lower-cased for both.
/// </summary>
internal sealed class StringRangeSubQueryTranslator : MeilisearchSubQueryTranslator<StringRange>
{
    protected override Result<MeilisearchFilter> Translate(StringRange query, IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators) =>
        Create(query);

    public static Result<MeilisearchFilter> Create(StringRange query)
    {
        var min = query.MinValue?.ToLowerInvariant();
        var max = query.MaxValue?.ToLowerInvariant();
        if (min != null && max != null)
        {
            // TO includes both edges: the string just after an excluded minimum, and the largest one before an excluded maximum.
            min = query.IncludeMinEdge ? min : min + '\0';
            max = query.IncludeMaxEdge ? max : RangeFilter.Before(max);
            if (max == null)
            {
                var field = MeilisearchFieldNames.Require(query.Field, nameof(StringRange));
                return field.IsFailed ? field.ToResult<MeilisearchFilter>() : Filter(MeilisearchFieldNames.MatchNone);
            }
        }

        return RangeFilter.Create(query, nameof(StringRange), MeilisearchFieldNames.Tokens,
            min == null ? null : MeilisearchFieldNames.Quote(min), max == null ? null : MeilisearchFieldNames.Quote(max));
    }
}

internal static class RangeFilter
{
    /// <summary>The highest code point, above any character a word or value holds in practice.</summary>
    private const string Highest = "\U0010FFFF";

    /// <summary>
    /// A range over numbers, which Meilisearch stores as doubles. With both bounds an excluded edge moves to the next
    /// double inward, as <c>TO</c> includes its edges.
    /// </summary>
    public static Result<MeilisearchFilter> Numbers(BaseRangeQuery query, string queryName, string map, double? min, double? max)
    {
        var finite = RangeBounds.RequireFinite(queryName, min, max);
        if (finite.IsFailed)
        {
            return finite.ToResult<MeilisearchFilter>();
        }

        if (min != null && max != null)
        {
            min = query.IncludeMinEdge ? min : Math.BitIncrement(min.Value);
            max = query.IncludeMaxEdge ? max : Math.BitDecrement(max.Value);
            if (double.IsInfinity(min.Value) || double.IsInfinity(max.Value))
            {
                // Nothing lies above an excluded double.MaxValue or below an excluded double.MinValue.
                var field = MeilisearchFieldNames.Require(query.Field, queryName);
                return field.IsFailed ? field.ToResult<MeilisearchFilter>() : Result.Ok(new MeilisearchFilter(MeilisearchFieldNames.MatchNone));
            }
        }

        return Create(query, queryName, map, MeilisearchFieldNames.Number(min), MeilisearchFieldNames.Number(max));
    }

    /// <summary>A range filter, failing when <paramref name="min"/> and <paramref name="max"/> are both <c>null</c>.</summary>
    public static Result<MeilisearchFilter> Create(BaseRangeQuery query, string queryName, string map, string? min, string? max)
    {
        var field = MeilisearchFieldNames.Require(query.Field, queryName);
        if (field.IsFailed)
        {
            return field.ToResult<MeilisearchFilter>();
        }

        var bounds = RangeBounds.Require(queryName, min, max);
        return bounds.IsFailed
            ? bounds.ToResult<MeilisearchFilter>()
            : Result.Ok(new MeilisearchFilter(MeilisearchFieldNames.Range(
                MeilisearchFieldNames.In(map, field.Value), min, query.IncludeMinEdge, max, query.IncludeMaxEdge)));
    }

    /// <summary>
    /// The largest string before <paramref name="value"/> as Meilisearch orders lower-cased strings, by code point, or
    /// <c>null</c> for the empty string, which nothing precedes. The last character steps down to a lower-case one,
    /// followed by <see cref="Highest"/>.
    /// </summary>
    public static string? Before(string value)
    {
        if (value.Length == 0)
        {
            return null;
        }

        Rune.DecodeLastFromUtf16(value, out var last, out var length);
        var prefix = value[..^length];
        if (last.Value == 0)
        {
            return prefix;
        }

        var code = last.Value - 1;
        while (true)
        {
            if (code is >= 0xD800 and <= 0xDFFF)
            {
                code = 0xD7FF;
            }

            var rune = new Rune(code);
            if (Rune.ToLowerInvariant(rune) == rune)
            {
                return prefix + rune + Highest;
            }

            code--;
        }
    }
}
