using Bielu.BetterSearch.Services;
using FluentResults;

namespace Bielu.BetterSearch.Meilisearch.Services;

/// <summary>
/// Builds Meilisearch filter expressions over the attributes <see cref="MeilisearchDocument"/> stores.
/// A field name already starting with <c>fields.</c> filters on the stored value as given.
/// </summary>
internal static class MeilisearchFieldNames
{
    public const string Fields = "fields";
    public const string Tokens = "tokens";
    public const string Phrases = "phrases";
    public const string Dates = "dates";
    public const string Geo = "_geo";

    /// <summary>Matches every document: each one has an id.</summary>
    public const string MatchAll = "id EXISTS";

    /// <summary>Matches no document.</summary>
    public const string MatchNone = "id IN []";

    public const string FieldsPrefix = Fields + ".";

    public static readonly string[] FilterableAttributes = ["id", "type", Fields, Tokens, Phrases, Dates, Geo];
    public static readonly string[] SortableAttributes = [Fields];

    /// <summary>Whether <paramref name="field"/> names the stored value rather than its words.</summary>
    public static bool IsStoredValue(string field) => field.StartsWith(FieldsPrefix, StringComparison.Ordinal);

    /// <summary>
    /// The attribute <paramref name="field"/> is stored under in <paramref name="map"/>. Dates are only comparable as
    /// Unix milliseconds, so a date on a <c>fields.</c> name still compares its <c>dates</c> copy.
    /// </summary>
    public static string In(string map, string field) => IsStoredValue(field)
        ? map == Dates ? $"{Dates}.{field[FieldsPrefix.Length..]}" : field
        : $"{map}.{field}";

    /// <summary>
    /// The query's field, or a failure when it is missing or cannot be written in a filter: Meilisearch
    /// attribute names in a filter are unquoted, so only letters, digits, <c>_</c>, <c>-</c> and <c>.</c> are allowed.
    /// </summary>
    public static Result<string> Require(string? field, string queryName)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return Result.Fail<string>($"{queryName} needs a Field for Meilisearch.");
        }

        return field.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.')
            ? Result.Ok(field)
            : Result.Fail<string>($"{queryName} field '{field}' can only contain letters, digits, '_', '-' and '.' for Meilisearch.");
    }

    public static string Quote(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    /// <summary>
    /// A number as a filter literal, or <c>null</c> when <paramref name="value"/> is not a number. The text is
    /// <see cref="QueryValueText"/>'s, in the invariant culture, except that Meilisearch filters read <c>1E20</c> but not
    /// <c>1E+20</c>, so a positive exponent drops its <c>+</c>.
    /// </summary>
    public static string? Number(object? value) => QueryValueText.IsNumber(value)
        ? QueryValueText.From(value).Replace("E+", "E", StringComparison.Ordinal)
        : null;

    /// <summary>
    /// A failure when <paramref name="value"/> is <c>NaN</c> or an infinity, which a Meilisearch filter cannot hold:
    /// it reads <c>NaN</c> and <c>Infinity</c> as text, so the filter would match those strings rather than a number.
    /// </summary>
    public static Result RequireFinite(object? value, string queryName) =>
        value is double d && !double.IsFinite(d) || value is float f && !float.IsFinite(f)
            ? Result.Fail($"{queryName} value {QueryValueText.From(value)} is not a finite number, which Meilisearch filters cannot compare.")
            : Result.Ok();

    /// <summary>
    /// Values of <paramref name="attribute"/> between the bounds present; <c>EXISTS</c> when neither is. With both
    /// bounds this is one <c>TO</c> range, so on a field with many values one value must lie inside it, as on
    /// Elasticsearch; two comparisons could each be met by a different value. <c>TO</c> includes both edges, so the
    /// caller passes the closest included value for an excluded edge.
    /// </summary>
    public static string Range(string attribute, string? min, bool includeMin, string? max, bool includeMax)
    {
        if (min != null && max != null)
        {
            return $"{attribute} {min} TO {max}";
        }

        if (min != null)
        {
            return $"{attribute} {(includeMin ? ">=" : ">")} {min}";
        }

        return max != null ? $"{attribute} {(includeMax ? "<=" : "<")} {max}" : $"{attribute} EXISTS";
    }
}
