using System.Globalization;

namespace Bielu.BetterSearch.Lifti.Services;

/// <summary>Turns field and query values into text the same way, with the invariant culture, so an indexed value and a query for it always agree.</summary>
internal static class LiftiText
{
    public static string From(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
}
