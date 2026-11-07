using System.Globalization;
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

    private const string FieldsPrefix = Fields + ".";

    public static readonly string[] FilterableAttributes = ["id", "type", Fields, Tokens, Phrases, Dates, Geo];
    public static readonly string[] SortableAttributes = [Fields];

    /// <summary>Whether <paramref name="field"/> names the stored value rather than its words.</summary>
    public static bool IsStoredValue(string field) => field.StartsWith(FieldsPrefix, StringComparison.Ordinal);

    /// <summary>The attribute <paramref name="field"/> is stored under in <paramref name="map"/>.</summary>
    public static string In(string map, string field) =>
        IsStoredValue(field) ? field : $"{map}.{field}";

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

    /// <summary>A number as a filter literal, or <c>null</c> when <paramref name="value"/> is not a number.</summary>
    public static string? Number(object? value) => value switch
    {
        byte or sbyte or short or ushort or int or uint or long or ulong or decimal =>
            Convert.ToString(value, CultureInfo.InvariantCulture),
        float number => number.ToString("R", CultureInfo.InvariantCulture),
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        _ => null
    };

    /// <summary>A comparison of <paramref name="attribute"/> with each bound present; <c>EXISTS</c> when neither is.</summary>
    public static string Range(string attribute, string? min, bool includeMin, string? max, bool includeMax)
    {
        var bounds = new List<string>();
        if (min != null)
        {
            bounds.Add($"{attribute} {(includeMin ? ">=" : ">")} {min}");
        }

        if (max != null)
        {
            bounds.Add($"{attribute} {(includeMax ? "<=" : "<")} {max}");
        }

        return bounds.Count == 0 ? $"{attribute} EXISTS" : string.Join(" AND ", bounds);
    }
}
