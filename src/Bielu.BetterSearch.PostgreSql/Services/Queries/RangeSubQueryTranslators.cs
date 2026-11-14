using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Services;
using FluentResults;
using NpgsqlTypes;

namespace Bielu.BetterSearch.PostgreSql.Services.Queries;

/// <summary>
/// A range matches when a single value of the field is within both bounds, so a field holding 10 and 60 does not
/// match 20 to 50. Numbers compare with numbers only, dates with dates and strings with strings.
/// </summary>
internal static class RangeFilter
{
    /// <summary>The condition on one element <c>e</c> of the field's values, and both bounds compared with <paramref name="value"/>.</summary>
    public static PostgreSqlFilter Build(string field, string queryName, string elementCondition, string value,
        object? min, object? max, NpgsqlDbType type, bool includeMin, bool includeMax) =>
        new PostgreSqlFilter.Condition($"{queryName} on {field}", p =>
        {
            var bounds = new List<string> { elementCondition };
            if (min != null)
            {
                bounds.Add($"{value} {(includeMin ? ">=" : ">")} {p.Add(min, type)}");
            }

            if (max != null)
            {
                bounds.Add($"{value} {(includeMax ? "<=" : "<")} {p.Add(max, type)}");
            }

            return $"EXISTS (SELECT 1 FROM {p.Elements(field)} e WHERE {string.Join(" AND ", bounds)})";
        });

    public static Result<string> Field(string? field, string queryName) =>
        string.IsNullOrWhiteSpace(field) ? Result.Fail<string>($"{queryName} needs a Field.") : Result.Ok(field);

    /// <summary>
    /// The element as a <c>timestamptz</c> when it is a string starting with an ISO 8601 date, else <c>NULL</c>. A
    /// date or time without an offset is UTC, as <see cref="QueryValueText"/> writes dates.
    /// </summary>
    public const string Timestamp =
        "(CASE WHEN jsonb_typeof(e) = 'string' AND (e #>> '{}') ~ '^\\d{4}-\\d{2}-\\d{2}' AND pg_input_is_valid(e #>> '{}', 'timestamptz') " +
        "THEN CASE WHEN (e #>> '{}') ~ '([Zz]|[+-]\\d{2}(:?\\d{2})?)$' THEN (e #>> '{}')::timestamptz " +
        "ELSE (e #>> '{}')::timestamp AT TIME ZONE 'UTC' END END)";
}

internal sealed class NumericRangeSubQueryTranslator : PostgreSqlSubQueryTranslator<NumericRange>
{
    protected override Result<PostgreSqlFilter> Translate(NumericRange query)
    {
        var field = RangeFilter.Field(query.Field, nameof(NumericRange));
        var bounds = Result.Merge(field, RangeBounds.Require(nameof(NumericRange), query.MinValue, query.MaxValue),
            RangeBounds.RequireFinite(nameof(NumericRange), query.MinValue, query.MaxValue));
        return bounds.IsFailed
            ? Result.Fail<PostgreSqlFilter>(bounds.Errors[0])
            : Result.Ok(RangeFilter.Build(field.Value, nameof(NumericRange), "jsonb_typeof(e) = 'number'", "(e)::numeric",
                query.MinValue, query.MaxValue, NpgsqlDbType.Double, query.IncludeMinEdge, query.IncludeMaxEdge));
    }
}

/// <summary>Compares exactly at any size, as the bounds are compared with the stored decimals as integers.</summary>
internal sealed class LongRangeSubQueryTranslator : PostgreSqlSubQueryTranslator<LongRange>
{
    protected override Result<PostgreSqlFilter> Translate(LongRange query)
    {
        var field = RangeFilter.Field(query.Field, nameof(LongRange));
        var bounds = Result.Merge(field, RangeBounds.Require(nameof(LongRange), query.MinValue, query.MaxValue));
        return bounds.IsFailed
            ? Result.Fail<PostgreSqlFilter>(bounds.Errors[0])
            : Result.Ok(RangeFilter.Build(field.Value, nameof(LongRange), "jsonb_typeof(e) = 'number'", "(e)::numeric",
                query.MinValue, query.MaxValue, NpgsqlDbType.Bigint, query.IncludeMinEdge, query.IncludeMaxEdge));
    }
}

/// <summary>Compares instants, so a bound with any offset or <c>DateTimeKind</c> matches the dates stored as UTC text.</summary>
internal sealed class DateRangeSubQueryTranslator : PostgreSqlSubQueryTranslator<DateRangeQuery>
{
    protected override Result<PostgreSqlFilter> Translate(DateRangeQuery query)
    {
        var field = RangeFilter.Field(query.Field, nameof(DateRangeQuery));
        var bounds = Result.Merge(field, RangeBounds.Require(nameof(DateRangeQuery), query.MinValue, query.MaxValue));
        return bounds.IsFailed
            ? Result.Fail<PostgreSqlFilter>(bounds.Errors[0])
            : Result.Ok(RangeFilter.Build(field.Value, nameof(DateRangeQuery), $"{RangeFilter.Timestamp} IS NOT NULL", RangeFilter.Timestamp,
                query.MinValue?.UtcDateTime, query.MaxValue?.UtcDateTime, NpgsqlDbType.TimestampTz, query.IncludeMinEdge, query.IncludeMaxEdge));
    }
}

/// <summary>
/// Compares whole string values by their UTF-8 bytes (the <c>C</c> collation), as Elasticsearch compares a keyword
/// field, so <c>B</c> sorts before <c>a</c>.
/// </summary>
internal sealed class StringRangeSubQueryTranslator : PostgreSqlSubQueryTranslator<StringRange>
{
    protected override Result<PostgreSqlFilter> Translate(StringRange query)
    {
        var field = RangeFilter.Field(query.Field, nameof(StringRange));
        var bounds = Result.Merge(field, RangeBounds.Require(nameof(StringRange), query.MinValue, query.MaxValue));
        return bounds.IsFailed
            ? Result.Fail<PostgreSqlFilter>(bounds.Errors[0])
            : Result.Ok(RangeFilter.Build(field.Value, nameof(StringRange), "jsonb_typeof(e) = 'string'", "(e #>> '{}') COLLATE \"C\"",
                query.MinValue, query.MaxValue, NpgsqlDbType.Text, query.IncludeMinEdge, query.IncludeMaxEdge));
    }
}
