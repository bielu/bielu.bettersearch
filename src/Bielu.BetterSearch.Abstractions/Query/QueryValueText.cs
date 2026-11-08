using System.Globalization;

namespace Bielu.BetterSearch.Abstractions.Query;

/// <summary>
/// Turns a query or field value into text the same way on every machine, whatever its culture, so
/// providers that send values as text agree with each other.
/// </summary>
internal static class QueryValueText
{
    /// <summary>
    /// The value as text: booleans in lower case (<c>true</c>), and anything else with the invariant culture
    /// (<c>3.5</c>, never <c>3,5</c>). <c>null</c> is the empty string.
    /// </summary>
    public static string From(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        bool flag => flag ? "true" : "false",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };
}
