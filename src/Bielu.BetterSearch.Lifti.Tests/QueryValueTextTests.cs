using System.Globalization;
using Bielu.BetterSearch.Abstractions.Query;
using Shouldly;

namespace Bielu.BetterSearch.Lifti.Tests;

public class QueryValueTextTests
{
    public static TheoryData<object?, string> Values => new()
    {
        { null, "" },
        { "Jane", "Jane" },
        { true, "true" },
        { false, "false" },
        { 3.5, "3.5" },
        { 35, "35" },
        { new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc), "2026-11-08T00:00:00.0000000Z" },
        { new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.FromHours(1)), "2026-11-08T00:00:00.0000000+01:00" },
        { new DateOnly(2026, 11, 8), "2026-11-08" }
    };

    [Theory]
    [MemberData(nameof(Values))]
    public void From_CommaDecimalCulture_WritesInvariantText(object? value, string expected)
    {
        var culture = CultureInfo.CurrentCulture;
        var commaDecimal = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        commaDecimal.NumberFormat.NumberDecimalSeparator = ",";
        commaDecimal.DateTimeFormat.ShortDatePattern = "dd.MM.yyyy";
        try
        {
            CultureInfo.CurrentCulture = commaDecimal;
            QueryValueText.From(value).ShouldBe(expected);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}
