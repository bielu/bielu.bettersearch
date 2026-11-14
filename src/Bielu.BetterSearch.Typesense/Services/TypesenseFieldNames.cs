using System.Globalization;
using Bielu.BetterSearch.Services;
using FluentResults;

namespace Bielu.BetterSearch.Typesense.Services;

/// <summary>
/// The fields <see cref="TypesenseDocument"/> stores for each document field, one per kind of value, each declared in
/// the collection schema by a prefix pattern so Typesense knows its type before the first document. A field name
/// starting with <c>fields.</c> compares the whole stored string rather than its words.
/// </summary>
internal static class TypesenseFieldNames
{
    public const string Source = "source";
    public const string Type = "type";
    public const string Index = "index";

    /// <summary>Each word of a string value, lower-cased.</summary>
    public const string Tokens = "t_";

    /// <summary>Each string value, whole.</summary>
    public const string Strings = "s_";

    /// <summary>The runs of words a phrase prefix can start at.</summary>
    public const string Phrases = "p_";

    /// <summary>Each integer value, as an <c>int64</c>.</summary>
    public const string Integers = "i_";

    /// <summary>Each other number, as a Typesense <c>float</c>.</summary>
    public const string Numbers = "n_";

    /// <summary>Each date as Unix milliseconds.</summary>
    public const string Dates = "d_";

    /// <summary>Each boolean value.</summary>
    public const string Booleans = "b_";

    /// <summary>The point of <c>TypesenseOptions.GeoField</c>.</summary>
    public const string Geo = "g_";

    /// <summary>The value of a field holding one integer or date (as Unix milliseconds), which Typesense can sort on; arrays can't be sorted.</summary>
    public const string SortInteger = "oi_";

    /// <summary>The value of a field holding one other number, for sorting.</summary>
    public const string SortNumber = "on_";

    /// <summary>The value of a field holding one string, for sorting.</summary>
    public const string SortString = "os_";

    public const string FieldsPrefix = "fields.";

    /// <summary>The sort copies, in the order a sort prefers them.</summary>
    public static readonly string[] SortPrefixes = [SortInteger, SortNumber, SortString];

    /// <summary>The copies a field's values are stored in, other than its sort copies.</summary>
    public static readonly string[] ValuePrefixes = [Tokens, Integers, Numbers, Dates, Booleans, Geo];

    /// <summary>Whether <paramref name="field"/> names the stored value rather than its words.</summary>
    public static bool IsStoredValue(string field) => field.StartsWith(FieldsPrefix, StringComparison.Ordinal);

    /// <summary>The document field <paramref name="field"/> names, without a <c>fields.</c> prefix.</summary>
    public static string Name(string field) => IsStoredValue(field) ? field[FieldsPrefix.Length..] : field;

    /// <summary>
    /// The query's field, or a failure when it is missing or cannot be written in a filter. The provider prefixes each
    /// name and Typesense reads filter field names unquoted, so only letters, digits and <c>_</c> are allowed, after an
    /// optional <c>fields.</c> prefix.
    /// </summary>
    public static Result<string> Require(string? field, string queryName)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return Result.Fail<string>($"{queryName} needs a Field for Typesense.");
        }

        var name = Name(field);
        return name.Length > 0 && IsValidName(name)
            ? Result.Ok(field)
            : Result.Fail<string>($"{queryName} field '{field}' can only contain letters, digits and '_' for Typesense.");
    }

    /// <summary>Whether a document field name can be stored and filtered on.</summary>
    public static bool IsValidName(string name) => name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    /// <summary>A string as a filter literal, or a failure for a backtick, which Typesense filters cannot hold in a value.</summary>
    public static Result<string> Quote(string value, string queryName) =>
        value.Contains('`', StringComparison.Ordinal)
            ? Result.Fail<string>($"{queryName} value '{value}' holds a backtick, which a Typesense filter value cannot contain.")
            : Result.Ok($"`{value}`");

    public static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    public static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>A failure when <paramref name="value"/> is <c>NaN</c> or an infinity, which a Typesense filter cannot compare.</summary>
    public static Result RequireFinite(object? value, string queryName) =>
        value is double d && !double.IsFinite(d) || value is float f && !float.IsFinite(f)
            ? Result.Fail($"{queryName} value {QueryValueText.From(value)} is not a finite number, which Typesense filters cannot compare.")
            : Result.Ok();
}
