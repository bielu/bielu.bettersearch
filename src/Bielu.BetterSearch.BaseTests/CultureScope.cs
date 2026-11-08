using System.Globalization;

namespace Bielu.BetterSearch.Tests;

/// <summary>
/// Sets <see cref="CultureInfo.CurrentCulture"/> to a culture cloned from the invariant culture, so it exists
/// on every machine, including globalization-invariant ones, and puts the original back when disposed.
/// </summary>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _original = CultureInfo.CurrentCulture;

    public CultureScope(Action<CultureInfo> configure)
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        configure(culture);
        CultureInfo.CurrentCulture = culture;
    }

    /// <summary>A culture that writes 3.5 as "3,5" and dates day-first, as <c>pl-PL</c> does.</summary>
    public static CultureScope CommaDecimal() => new(culture =>
    {
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.DateTimeFormat.ShortDatePattern = "dd.MM.yyyy";
        culture.DateTimeFormat.DateSeparator = ".";
    });

    public void Dispose() => CultureInfo.CurrentCulture = _original;
}
