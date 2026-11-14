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
        { new DateTime(2026, 11, 8, 13, 5, 0, DateTimeKind.Utc), "2026-11-08T13:05:00Z" },
        { new DateTime(2026, 11, 8, 13, 5, 0, DateTimeKind.Unspecified), "2026-11-08T13:05:00Z" },
        { new DateTime(2026, 11, 8, 13, 5, 0, 123, DateTimeKind.Utc), "2026-11-08T13:05:00.123Z" },
        { new DateTime(2026, 11, 8, 13, 5, 0, 120, DateTimeKind.Utc), "2026-11-08T13:05:00.12Z" },
        { new DateTime(2026, 11, 8, 13, 5, 0, DateTimeKind.Utc).AddTicks(1), "2026-11-08T13:05:00.0000001Z" },
        { new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.FromHours(1)), "2026-11-07T23:00:00Z" },
        { new DateTimeOffset(2026, 11, 8, 13, 5, 0, 123, TimeSpan.Zero), "2026-11-08T13:05:00.123Z" },
        { new DateOnly(2026, 11, 8), "2026-11-08" },
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

    [Fact]
    public void From_LocalDateTime_WritesTheSameInstantInUtc()
    {
        var local = new DateTime(2026, 11, 8, 13, 5, 0, 123, DateTimeKind.Local);
        var utc = local.ToUniversalTime();

        From(local).ShouldBe(From(utc));
        From(utc).ShouldBe(utc.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.123Z'", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void From_DatesDifferingOnlyInMilliseconds_WriteDifferentText()
        => From(new DateTime(2026, 11, 8, 13, 5, 0, 1, DateTimeKind.Utc))
            .ShouldNotBe(From(new DateTime(2026, 11, 8, 13, 5, 0, 2, DateTimeKind.Utc)));
}
