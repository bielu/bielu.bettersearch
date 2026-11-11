using Bielu.BetterSearch.Meilisearch.Services;
using Bielu.BetterSearch.Services;
using Bielu.BetterSearch.Tests;
using Shouldly;

namespace Bielu.BetterSearch.Meilisearch.Tests;

public class MeilisearchFieldNamesTests
{
    // The shared text, then the filter literal, which differs only in dropping a positive exponent's +.
    public static TheoryData<object, string, string> Numbers => new()
    {
        { 35, "35", "35" },
        { 35L, "35", "35" },
        { (byte)35, "35", "35" },
        { 3.5m, "3.5", "3.5" },
        { 3.5, "3.5", "3.5" },
        { 3.5f, "3.5", "3.5" },
        { 1e20, "1E+20", "1E20" },
        { 1e-20, "1E-20", "1E-20" },
        { -1e20f, "-1E+20", "-1E20" }
    };

    [Theory]
    [MemberData(nameof(Numbers))]
    public void Number_CommaDecimalCulture_WritesSharedTextWithoutExponentPlus(object value, string sharedText, string expected)
    {
        using var _ = CultureScope.CommaDecimal();

        QueryValueText.From(value).ShouldBe(sharedText);
        MeilisearchFieldNames.Number(value).ShouldBe(expected);
    }

    [Theory]
    [InlineData("35")]
    [InlineData(true)]
    [InlineData(null)]
    public void Number_NotANumber_ReturnsNull(object? value) =>
        MeilisearchFieldNames.Number(value).ShouldBeNull();

    [Theory]
    [InlineData(double.NaN, "NaN")]
    [InlineData(double.PositiveInfinity, "Infinity")]
    [InlineData(float.NegativeInfinity, "-Infinity")]
    public void RequireFinite_NonFiniteNumber_ReturnsFailureNamingTheValue(object value, string text)
    {
        var result = MeilisearchFieldNames.RequireFinite(value, "TermSubQuery");

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe($"TermSubQuery value {text} is not a finite number, which Meilisearch filters cannot compare.");
    }

    [Theory]
    [InlineData(double.MaxValue)]
    [InlineData(float.MinValue)]
    [InlineData(35)]
    [InlineData("NaN")]
    [InlineData(null)]
    public void RequireFinite_FiniteNumberOrNotANumber_ReturnsOk(object? value) =>
        MeilisearchFieldNames.RequireFinite(value, "TermSubQuery").IsSuccess.ShouldBeTrue();
}
