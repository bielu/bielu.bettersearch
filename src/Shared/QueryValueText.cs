using System.Globalization;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// Turns a query or field value into text the same way on every machine, whatever its culture, so
/// providers that send values as text agree with each other.
/// </summary>
internal static class QueryValueText
{
    // ".FFFFFFF" writes the fraction without trailing zeros, and drops the dot too when the fraction is zero.
    private const string UtcDateTimeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFF'Z'";

    /// <summary>
    /// The value as text: booleans in lower case (<c>true</c>), dates in ISO 8601 as UTC with fractional seconds only
    /// when they are not zero (<c>2026-11-08T13:05:00Z</c>, <c>2026-11-08T13:05:00.123Z</c>), a date without a time
    /// zone taken as UTC, a <see cref="DateOnly"/> as <c>2026-11-08</c>, and anything else with the invariant culture
    /// (<c>3.5</c>, never <c>3,5</c>). <c>null</c> is the empty string.
    /// </summary>
    public static string From(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        bool flag => flag ? "true" : "false",
        DateTime { Kind: DateTimeKind.Unspecified } date => DateTime.SpecifyKind(date, DateTimeKind.Utc).ToString(UtcDateTimeFormat, CultureInfo.InvariantCulture),
        DateTime date => date.ToUniversalTime().ToString(UtcDateTimeFormat, CultureInfo.InvariantCulture),
        DateTimeOffset offset => offset.UtcDateTime.ToString(UtcDateTimeFormat, CultureInfo.InvariantCulture),
        DateOnly day => day.ToString("yyyy'-'MM'-'dd", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };

    /// <summary>Whether <paramref name="value"/> is a <see cref="DateTime"/>, <see cref="DateTimeOffset"/> or <see cref="DateOnly"/>.</summary>
    public static bool IsDate(object? value) => value is DateTime or DateTimeOffset or DateOnly;

    /// <summary>Whether <paramref name="value"/> is of a numeric type.</summary>
    public static bool IsNumber(object? value) =>
        value is byte or sbyte or short or ushort or int or uint or long or ulong or decimal or float or double;
}
