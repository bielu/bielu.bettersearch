namespace Bielu.BetterSearch.Abstractions.Fields;

/// <summary>
/// The plain document field culture filters use. Index each document's culture with <see cref="Value"/>
/// under <see cref="Name"/>, so <c>en-US</c> is stored as the single word <c>enus</c> every provider matches.
/// </summary>
public static class CultureField
{
    public const string Name = "Culture";

    /// <summary>The culture as stored: lower-cased, without <c>-</c> or <c>_</c>.</summary>
    public static string Value(string culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        return culture.Trim().ToLowerInvariant().Replace("-", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal);
    }
}
