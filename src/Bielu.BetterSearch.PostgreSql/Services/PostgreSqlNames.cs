using System.Text;
using System.Text.RegularExpressions;
using FluentResults;

namespace Bielu.BetterSearch.PostgreSql.Services;

/// <summary>
/// Turns index and schema names into quoted SQL identifiers. Values never go into SQL text: they are parameters, and
/// so are field names, which the queries read with <c>fields -&gt; $n</c>. The text search configuration is the one
/// name written into a table's definition, so it must match a strict pattern.
/// </summary>
internal static partial class PostgreSqlNames
{
    /// <summary>The longest identifier PostgreSQL keeps; a longer one is cut short, so two long names could share a table.</summary>
    public const int MaxIdentifierBytes = 63;

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex TextSearchConfigurationPattern();

    /// <summary>A failure when <paramref name="name"/>, at the setting <paramref name="path"/>, can't be a PostgreSQL identifier.</summary>
    public static string? IdentifierFailure(string path, string? name) => name switch
    {
        null or "" => $"{path} is missing.",
        _ when name.Contains('\0', StringComparison.Ordinal) => $"{path} holds a null character, which PostgreSQL identifiers can't contain.",
        _ when Encoding.UTF8.GetByteCount(name) > MaxIdentifierBytes => $"{path} is longer than {MaxIdentifierBytes} bytes, the longest name PostgreSQL keeps.",
        _ => null
    };

    public static string? TextSearchConfigurationFailure(string path, string? name) =>
        name != null && TextSearchConfigurationPattern().IsMatch(name)
            ? null
            : $"{path} '{name}' is not a text search configuration name: use letters, digits and underscores, optionally after a schema and a dot, for example simple or english.";

    /// <summary><paramref name="name"/> as a quoted identifier, with each double quote doubled.</summary>
    public static string Quote(string name) => $"\"{name.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    /// <summary>The quoted, schema-qualified name of the table holding <paramref name="index"/>.</summary>
    public static Result<string> Table(string schema, string? index)
    {
        if (IdentifierFailure("The index name", index) is { } failure)
        {
            return Result.Fail<string>(failure);
        }

        return Result.Ok($"{Quote(schema)}.{Quote(index!)}");
    }

    /// <summary><paramref name="text"/> as a string literal, for the text search configuration in a table's definition.</summary>
    public static string Literal(string text) => $"'{text.Replace("'", "''", StringComparison.Ordinal)}'";
}
