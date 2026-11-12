using Shouldly;

namespace Bielu.BetterSearch.Tests;

/// <summary>
/// Tests the <c>QueryValueText</c> each provider compiles from <c>src/Shared</c>. Each provider's test project
/// derives from this and passes its own copy, so the tests run against code that ships.
/// </summary>
public abstract class QueryValueTextTestBase
{
    public static TheoryData<object?, string> Values => new()
    {
        { null, "" },
        { "Jane", "Jane" },
        { true, "true" },
        { false, "false" },
        { 3.5, "3.5" },
        { 3.5f, "3.5" },
        { 3.5m, "3.5" },
        { 1e20, "1E+20" },
        { 35, "35" },
        { new DateTime(2026, 11, 8, 13, 5, 0, DateTimeKind.Utc), "11/08/2026 13:05:00" },
        { new DateTime(2026, 11, 8, 13, 5, 0, DateTimeKind.Local), "11/08/2026 13:05:00" },
        { new DateTime(2026, 11, 8, 13, 5, 0, DateTimeKind.Unspecified), "11/08/2026 13:05:00" },
        { new DateOnly(2026, 11, 8), "11/08/2026" },
        { new TimeOnly(13, 5), "13:05" }
    };

    /// <summary>The provider's <c>QueryValueText.From</c>.</summary>
    protected abstract string From(object? value);

    [Theory]
    [MemberData(nameof(Values))]
    public void From_CommaDecimalCulture_WritesInvariantText(object? value, string expected)
    {
        using var _ = CultureScope.CommaDecimal();
        From(value).ShouldBe(expected);
    }
}
