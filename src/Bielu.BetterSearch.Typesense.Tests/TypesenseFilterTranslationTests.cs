using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Typesense.Services;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bielu.BetterSearch.Typesense.Tests;

/// <summary>The filters the provider's translators build, rendered as if every field they name exists.</summary>
public sealed class TypesenseFilterTranslationTests(IServiceProvider services)
{
    [Fact]
    public async Task TermSubQuery_String_MatchesALowerCasedWord()
        => await ShouldRenderAsync(new TermSubQuery { Field = "Name", Value = "Jane" }, "t_Name:=[`jane`]");

    [Fact]
    public async Task TermSubQuery_StoredValue_MatchesTheWholeString()
        => await ShouldRenderAsync(new TermSubQuery { Field = "fields.Name", Value = "Jane Doe" }, "s_Name:=[`Jane Doe`]");

    [Fact]
    public async Task TermSubQuery_NumericString_AlsoMatchesTheNumber()
        => await ShouldRenderAsync(new TermSubQuery { Field = "Age", Value = "35" }, "(t_Age:=[`35`]) || (i_Age:=[35]) || (n_Age:=[35])");

    [Fact]
    public async Task TermSubQuery_Fraction_MatchesOnlyFloats()
        => await ShouldRenderAsync(new TermSubQuery { Field = "Price", Value = 3.5 }, "n_Price:=[3.5]");

    [Fact]
    public async Task TermSubQuery_LongAbove2To53_KeepsItsPrecisionOnIntegers()
        => await ShouldRenderAsync(new TermSubQuery { Field = "Big", Value = 9007199254740993L }, "(i_Big:=[9007199254740993]) || (n_Big:=[9007199254740992])");

    [Fact]
    public async Task TermSubQuery_Backtick_ReturnsFailedResult()
    {
        var result = await TranslateAsync(new TermSubQuery { Field = "Name", Value = "a`b" });

        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("holds a backtick");
    }

    [Fact]
    public async Task TermSubQuery_FieldWithDash_ReturnsFailedResult()
    {
        var result = await TranslateAsync(new TermSubQuery { Field = "first-name", Value = "jane" });

        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("can only contain letters, digits and '_'");
    }

    [Fact]
    public async Task TermsSubQuery_NoValues_MatchesNothing()
    {
        var result = await TranslateAsync(new TermsSubQuery { Field = "Name", Value = [] });

        result.Value.Render(_ => true).Value.MatchesNone.ShouldBeTrue();
    }

    [Fact]
    public async Task NumericRange_FractionalBounds_CompareIntegersInsideTheRange()
        => await ShouldRenderAsync(new NumericRange { Field = "Age", MinValue = 2.5, MaxValue = 7.5 }, "(i_Age:[3..7]) || (n_Age:[2.5..7.5])");

    [Fact]
    public async Task NumericRange_ExcludedWholeEdges_StepInward()
        => await ShouldRenderAsync(new NumericRange { Field = "Age", MinValue = 2, MaxValue = 7, IncludeMinEdge = false, IncludeMaxEdge = false },
            $"(i_Age:[3..6]) || (n_Age:[{(double)MathF.BitIncrement(2f)}..{(double)MathF.BitDecrement(7f)}])");

    [Fact]
    public async Task LongRange_MinimumOnly_ComparesFromTheMinimum()
        => await ShouldRenderAsync(new LongRange { Field = "Big", MinValue = 9007199254740993L }, "(i_Big:>=9007199254740993) || (n_Big:>=9007199254740992)");

    [Fact]
    public async Task DateRange_ExcludedEdges_StepByOneMillisecond()
    {
        var min = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var max = min.AddDays(1);
        await ShouldRenderAsync(new DateRangeQuery { Field = "Born", MinValue = min, MaxValue = max, IncludeMinEdge = false, IncludeMaxEdge = false },
            $"d_Born:[{min.ToUnixTimeMilliseconds() + 1}..{max.ToUnixTimeMilliseconds() - 1}]");
    }

    [Fact]
    public async Task PrefixPhraseSubQuery_Words_MatchesAStoredRunStartingWithThem()
        => await ShouldRenderAsync(new PrefixPhraseSubQuery { Field = "Name", Value = "Jane Do" }, "p_Name:=`jane do`*");

    [Fact]
    public async Task SpatialSearchQuery_Miles_ConvertsToKilometres()
        => await ShouldRenderAsync(new SpatialSearchQuery { Field = "Location", Distance = 1, UnitOfDistance = "mi", Lat = 52.23, Lng = 21.01 },
            "g_Location:(52.23, 21.01, 1.609344 km)");

    private async Task<Result<TypesenseFilter>> TranslateAsync(ISearchSubQuery subQuery)
    {
        using var scope = services.CreateScope();
        var translators = scope.ServiceProvider.GetServices<ISubQueryTranslator<TypesenseFilter>>().ToList();
        return await translators.First(translator => translator.CanTranslate(subQuery)).TranslateAsync(subQuery, translators);
    }

    private async Task ShouldRenderAsync(ISearchSubQuery subQuery, string expected)
    {
        var result = await TranslateAsync(subQuery);
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        (result.Value.Render(_ => true).Value.Expression ?? string.Empty).ShouldBe(expected);
    }
}
