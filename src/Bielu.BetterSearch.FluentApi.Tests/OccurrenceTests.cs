using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.FluentApi.Fluent;
using Bielu.BetterSearch.FluentApi.Fluent.Extensions;
using Shouldly;

namespace Bielu.BetterSearch.FluentApi.Tests;

public class OccurrenceTests
{
    [Fact]
    public void CreateTermQuery_NestedWithAndWithoutOwnOccurrence_OwnWinsElseParent() =>
        AssertNested(b => b
            .CreateTermQuery(t => t.WithField("City").WithValue("Warsaw").Should())
            .CreateTermQuery(t => t.WithField("City").WithValue("Krakow")));

    [Fact]
    public void CreateTermsQuery_NestedWithAndWithoutOwnOccurrence_OwnWinsElseParent() =>
        AssertNested(b => b
            .CreateTermsQuery(t => t.WithField("City").WithValue("Warsaw").Should())
            .CreateTermsQuery(t => t.WithField("City").WithValue("Krakow")));

    [Fact]
    public void CreateFuzzyQuery_NestedWithAndWithoutOwnOccurrence_OwnWinsElseParent() =>
        AssertNested(b => b
            .CreateFuzzyQuery(f => f.WithField("City").WithValue("Warsaw").Should())
            .CreateFuzzyQuery(f => f.WithField("City").WithValue("Krakow")));

    [Fact]
    public void CreatePrefixQuery_NestedWithAndWithoutOwnOccurrence_OwnWinsElseParent() =>
        AssertNested(b => b
            .CreatePrefixQuery(p => p.WithField("City").WithValue("War").Should())
            .CreatePrefixQuery(p => p.WithField("City").WithValue("Kra")));

    [Fact]
    public void CreatePrefixPhraseQuery_NestedWithAndWithoutOwnOccurrence_OwnWinsElseParent() =>
        AssertNested(b => b
            .CreatePrefixPhraseQuery(p => p.WithField("City").WithValue("War").Should())
            .CreatePrefixPhraseQuery(p => p.WithField("City").WithValue("Kra")));

    [Fact]
    public void CreateNumericRangeQuery_NestedWithAndWithoutOwnOccurrence_OwnWinsElseParent() =>
        AssertNested(b => b
            .CreateNumericRangeQuery(r => r.WithField("Age").WithMinValue(18).Should())
            .CreateNumericRangeQuery(r => r.WithField("Age").WithMinValue(30)));

    [Fact]
    public void CreateLongRangeQuery_NestedWithAndWithoutOwnOccurrence_OwnWinsElseParent() =>
        AssertNested(b => b
            .CreateLongRangeQuery(r => r.WithField("Views").WithMinValue(18L).Should())
            .CreateLongRangeQuery(r => r.WithField("Views").WithMinValue(30L)));

    [Fact]
    public void CreateStringRangeQuery_NestedWithAndWithoutOwnOccurrence_OwnWinsElseParent() =>
        AssertNested(b => b
            .CreateStringRangeQuery(r => r.WithField("Name").WithMinValue("a").Should())
            .CreateStringRangeQuery(r => r.WithField("Name").WithMinValue("m")));

    [Fact]
    public void CreateDateRangeQuery_NestedWithAndWithoutOwnOccurrence_OwnWinsElseParent() =>
        AssertNested(b => b
            .CreateDateRangeQuery(r => r.WithField("Born").WithMinValue(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Should())
            .CreateDateRangeQuery(r => r.WithField("Born").WithMinValue(new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc))));

    [Fact]
    public void CreateGoeDistanceQuery_NestedWithAndWithoutOwnOccurrence_OwnWinsElseParent() =>
        AssertNested(b => b
            .CreateGoeDistanceQuery(s => s.WithField("Location").WithDistance(5).Should())
            .CreateGoeDistanceQuery(s => s.WithField("Location").WithDistance(10)));

    [Fact]
    public void CreateTermQuery_TopLevelWithAndWithoutOwnOccurrence_OwnWinsElseParent()
    {
        var query = Build(q => q
            .CreateTermQuery(t => t.WithField("City").WithValue("Warsaw").Should())
            .CreateTermQuery(t => t.WithField("City").WithValue("Krakow")));

        AssertTopLevel(query, "Warsaw", "Krakow");
    }

    [Fact]
    public void CreateTermsQuery_TopLevelWithAndWithoutOwnOccurrence_OwnWinsElseParent()
    {
        var query = Build(q => q
            .CreateTermsQuery(t => t.WithField("City").WithValue("Warsaw").Should())
            .CreateTermsQuery(t => t.WithField("City").WithValue("Krakow")));

        query.Query.Keys.ShouldBe([Occurance.SHOULD, Occurance.MUST], ignoreOrder: true);
        var should = query.Query[Occurance.SHOULD].ShouldHaveSingleItem().ShouldBeOfType<TermsSubQuery>();
        should.Value.ShouldBe(["Warsaw"]);
        should.Occurance.ShouldBe(Occurance.SHOULD);
        var must = query.Query[Occurance.MUST].ShouldHaveSingleItem().ShouldBeOfType<TermsSubQuery>();
        must.Value.ShouldBe(["Krakow"]);
        must.Occurance.ShouldBe(Occurance.MUST);
    }

    [Fact]
    public void CreateTermQuery_OwnMustUnderShouldParent_IsAddedAsMust()
    {
        var nested = BuildNested(b => b.Should()
            .CreateTermQuery(t => t.WithField("City").WithValue("Warsaw").Must())
            .CreateTermQuery(t => t.WithField("City").WithValue("Krakow")));

        nested.NestedQueries.Select(q => q.Occurance).ShouldBe([Occurance.MUST, Occurance.SHOULD]);
    }

    [Fact]
    public void CreateTermQuery_OwnMustNotAndFilter_AreHonoured()
    {
        var nested = BuildNested(b => b
            .CreateTermQuery(t => t.WithField("City").WithValue("Warsaw").MustNot())
            .CreateTermQuery(t => t.WithField("City").WithValue("Krakow").Filter()));

        nested.NestedQueries.Select(q => q.Occurance).ShouldBe([Occurance.MUSTNOT, Occurance.FILTER]);
    }

    [Fact]
    public void CreateBoolQuery_NestedWithoutOccurrence_ChildrenAreAddedAsMust()
    {
        var outer = BuildNested(b => b.CreateBoolQuery(inner => inner
            .CreateTermQuery(t => t.WithField("City").WithValue("Warsaw"))));

        var inner = outer.NestedQueries.ShouldHaveSingleItem().ShouldBeOfType<BoolSearchSubQuery>();
        inner.Occurance.ShouldBe(Occurance.MUST);
        inner.NestedQueries.ShouldHaveSingleItem().Occurance.ShouldBe(Occurance.MUST);
    }

    [Fact]
    public void BooleanQueryConfigurator_New_OccuranceIsMust() =>
        new BooleanQueryConfigurator().Occurance.ShouldBe(Occurance.MUST);

    [Fact]
    public void CreateBoolQuery_TopLevelUnderShouldParent_BoolIsAddedAsShould()
    {
        var query = Build(q =>
        {
            q.Occurance = Occurance.SHOULD;
            q.CreateBoolQuery(b => b.CreateTermQuery(t => t.WithField("City").WithValue("Warsaw")));
        });

        query.Query.Keys.ShouldBe([Occurance.SHOULD]);
        query.Query[Occurance.SHOULD].ShouldHaveSingleItem().Occurance.ShouldBe(Occurance.SHOULD);
    }

    // The parent bool is MUST; the first clause chose SHOULD and the second chose nothing.
    private static void AssertNested(Action<BooleanQueryConfigurator> configure)
    {
        var nested = BuildNested(b => configure(b.Must()));

        nested.NestedQueries.Select(q => q.Occurance).ShouldBe([Occurance.SHOULD, Occurance.MUST]);
    }

    private static BoolSearchSubQuery BuildNested(Action<BooleanQueryConfigurator> configure)
    {
        var query = Build(q => q.CreateBoolQuery(configure));

        return query.Query[Occurance.MUST].ShouldHaveSingleItem().ShouldBeOfType<BoolSearchSubQuery>();
    }

    private static void AssertTopLevel(ISearchQuery query, string shouldValue, string mustValue)
    {
        query.Query.Keys.ShouldBe([Occurance.SHOULD, Occurance.MUST], ignoreOrder: true);
        var should = query.Query[Occurance.SHOULD].ShouldHaveSingleItem().ShouldBeOfType<TermSubQuery>();
        should.Value.ShouldBe(shouldValue);
        should.Occurance.ShouldBe(Occurance.SHOULD);
        var must = query.Query[Occurance.MUST].ShouldHaveSingleItem().ShouldBeOfType<TermSubQuery>();
        must.Value.ShouldBe(mustValue);
        must.Occurance.ShouldBe(Occurance.MUST);
    }

    private static ISearchQuery Build(Action<IBaseQueryConfigurator> configure) =>
        FluentApiSearchQueryCreator.Create().CreateSearchQuery(configure);
}
