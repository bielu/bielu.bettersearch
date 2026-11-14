using Bielu.BetterSearch.Typesense.Services;
using Shouldly;

namespace Bielu.BetterSearch.Typesense.Tests;

public sealed class TypesenseFilterTests
{
    private static readonly TypesenseFilter Jane = new TypesenseFilter.Comparison("t_Name", "t_Name:=[`jane`]", "t_Name:!=[`jane`]");
    private static readonly TypesenseFilter John = new TypesenseFilter.Comparison("t_Name", "t_Name:=[`john`]", "t_Name:!=[`john`]");
    private static readonly TypesenseFilter Old = new TypesenseFilter.Comparison("i_Age", "i_Age:>=40", null, "no range negation");
    private static readonly TypesenseFilter Missing = new TypesenseFilter.Comparison("t_Missing", "t_Missing:=[`x`]", "t_Missing:!=[`x`]");

    private static Rendered Render(TypesenseFilter filter) =>
        filter.Render(field => field != "t_Missing").Value;

    [Fact]
    public void Render_NotOfAnd_PushesTheNegationDownByDeMorgan() =>
        Render(new TypesenseFilter.Not(new TypesenseFilter.And([Jane, John]))).Expression.ShouldBe("(t_Name:!=[`jane`]) || (t_Name:!=[`john`])");

    [Fact]
    public void Render_DoubleNegation_IsThePlainComparison() =>
        Render(new TypesenseFilter.Not(new TypesenseFilter.Not(Jane))).Expression.ShouldBe("t_Name:=[`jane`]");

    [Fact]
    public void Render_MissingField_MatchesNothing() =>
        Render(Missing).MatchesNone.ShouldBeTrue();

    [Fact]
    public void Render_NegatedMissingField_MatchesEverything() =>
        Render(new TypesenseFilter.Not(Missing)).MatchesAll.ShouldBeTrue();

    [Fact]
    public void Render_OrWithMissingField_KeepsTheOtherClause() =>
        Render(new TypesenseFilter.Or([Missing, Jane])).Expression.ShouldBe("t_Name:=[`jane`]");

    [Fact]
    public void Render_AndWithMissingField_MatchesNothing() =>
        Render(new TypesenseFilter.And([Missing, Jane])).MatchesNone.ShouldBeTrue();

    [Fact]
    public void Render_NegatedRange_ReturnsFailedResultWithTheReason() =>
        new TypesenseFilter.Not(Old).Render(_ => true).Errors.ShouldHaveSingleItem().Message.ShouldBe("no range negation");

    [Fact]
    public void Render_MustNotOnly_IsTheNegation() =>
        Render(TypesenseBoolFilterBuilder.Combine([], [], [Jane])).Expression.ShouldBe("t_Name:!=[`jane`]");

    [Fact]
    public void Render_ShouldBesideMust_OnlyRequiresTheMust() =>
        Render(TypesenseBoolFilterBuilder.Combine([Jane], [John], [])).Expression.ShouldBe("t_Name:=[`jane`]");

    [Fact]
    public void Render_NoClauses_MatchesEverything() =>
        Render(TypesenseBoolFilterBuilder.Combine([], [], [])).MatchesAll.ShouldBeTrue();
}
