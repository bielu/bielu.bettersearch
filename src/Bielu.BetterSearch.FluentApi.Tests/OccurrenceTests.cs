using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.FluentApi.Fluent;
using Bielu.BetterSearch.FluentApi.Fluent.Extensions;
using Bielu.BetterSearch.FluentApi.Fluent.RangeQueries;
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
    public void CreateGeoDistanceQuery_NestedWithAndWithoutOwnOccurrence_OwnWinsElseParent() =>
        AssertNested(b => b
            .CreateGeoDistanceQuery(s => s.WithField("Location").WithDistance(5).Should())
            .CreateGeoDistanceQuery(s => s.WithField("Location").WithDistance(10)));

    [Fact]
    public void CreateLuceneQuery_NestedWithAndWithoutOwnOccurrence_OwnWinsElseParent() =>
        AssertNested(b => b
            .CreateLuceneQuery(l => l.WithQuery("City:Warsaw").Should())
            .CreateLuceneQuery(l => l.WithQuery("City:Krakow")));

    [Fact]
    public void CreateFuzzyQuery_TopLevelWithAndWithoutOwnOccurrence_OwnWinsElseParent() =>
        AssertTopLevel<FuzzySubQuery>(q => q
            .CreateFuzzyQuery(f => f.WithField("City").WithValue("Warsaw").Should())
            .CreateFuzzyQuery(f => f.WithField("City").WithValue("Krakow")));

    [Fact]
    public void CreatePrefixQuery_TopLevelWithAndWithoutOwnOccurrence_OwnWinsElseParent() =>
        AssertTopLevel<PrefixSubQuery>(q => q
            .CreatePrefixQuery(p => p.WithField("City").WithValue("War").Should())
            .CreatePrefixQuery(p => p.WithField("City").WithValue("Kra")));

    [Fact]
    public void CreatePrefixPhraseQuery_TopLevelWithAndWithoutOwnOccurrence_OwnWinsElseParent() =>
        AssertTopLevel<PrefixPhraseSubQuery>(q => q
            .CreatePrefixPhraseQuery(p => p.WithField("City").WithValue("War").Should())
            .CreatePrefixPhraseQuery(p => p.WithField("City").WithValue("Kra")));

    [Fact]
    public void CreateNumericRangeQuery_TopLevelWithAndWithoutOwnOccurrence_OwnWinsElseParent() =>
        AssertTopLevel<NumericRangeSubQuery>(q => q
            .CreateNumericRangeQuery(r => r.WithField("Age").WithMinValue(18).Should())
            .CreateNumericRangeQuery(r => r.WithField("Age").WithMinValue(30)));

    [Fact]
    public void CreateLongRangeQuery_TopLevelWithAndWithoutOwnOccurrence_OwnWinsElseParent() =>
        AssertTopLevel<LongRangeSubQuery>(q => q
            .CreateLongRangeQuery(r => r.WithField("Views").WithMinValue(18L).Should())
            .CreateLongRangeQuery(r => r.WithField("Views").WithMinValue(30L)));

    [Fact]
    public void CreateStringRangeQuery_TopLevelWithAndWithoutOwnOccurrence_OwnWinsElseParent() =>
        AssertTopLevel<StringRangeSubQuery>(q => q
            .CreateStringRangeQuery(r => r.WithField("Name").WithMinValue("a").Should())
            .CreateStringRangeQuery(r => r.WithField("Name").WithMinValue("m")));

    [Fact]
    public void CreateDateRangeQuery_TopLevelWithAndWithoutOwnOccurrence_OwnWinsElseParent() =>
        AssertTopLevel<DateRangeSubQuery>(q => q
            .CreateDateRangeQuery(r => r.WithField("Born").WithMinValue(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Should())
            .CreateDateRangeQuery(r => r.WithField("Born").WithMinValue(new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc))));

    [Fact]
    public void CreateGeoDistanceQuery_TopLevelWithAndWithoutOwnOccurrence_OwnWinsElseParent() =>
        AssertTopLevel<SpatialSearchSubQuery>(q => q
            .CreateGeoDistanceQuery(s => s.WithField("Location").WithDistance(5).Should())
            .CreateGeoDistanceQuery(s => s.WithField("Location").WithDistance(10)));

    [Fact]
    public void CreateLuceneQuery_TopLevelWithAndWithoutOwnOccurrence_OwnWinsElseParent() =>
        AssertTopLevel<LuceneSubQuery>(q => q
            .CreateLuceneQuery(l => l.WithQuery("City:Warsaw").Should())
            .CreateLuceneQuery(l => l.WithQuery("City:Krakow")));

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

        query.Query.Select(clause => clause.Occurrence).Distinct().ShouldBe([Occurrence.Should, Occurrence.Must], ignoreOrder: true);
        var should = query.Query.Where(clause => clause.Occurrence == Occurrence.Should).ToList().ShouldHaveSingleItem().ShouldBeOfType<TermsSubQuery>();
        should.Value.ShouldBe(["Warsaw"]);
        should.Occurrence.ShouldBe(Occurrence.Should);
        var must = query.Query.Where(clause => clause.Occurrence == Occurrence.Must).ToList().ShouldHaveSingleItem().ShouldBeOfType<TermsSubQuery>();
        must.Value.ShouldBe(["Krakow"]);
        must.Occurrence.ShouldBe(Occurrence.Must);
    }

    [Fact]
    public void CreateTermQuery_OwnMustUnderShouldParent_IsAddedAsMust()
    {
        var nested = BuildNested(b => b.Should()
            .CreateTermQuery(t => t.WithField("City").WithValue("Warsaw").Must())
            .CreateTermQuery(t => t.WithField("City").WithValue("Krakow")));

        nested.NestedQueries.Select(q => q.Occurrence).ShouldBe([Occurrence.Must, Occurrence.Should]);
    }

    [Fact]
    public void CreateTermQuery_OwnMustNotAndFilter_AreHonoured()
    {
        var nested = BuildNested(b => b
            .CreateTermQuery(t => t.WithField("City").WithValue("Warsaw").MustNot())
            .CreateTermQuery(t => t.WithField("City").WithValue("Krakow").Filter()));

        nested.NestedQueries.Select(q => q.Occurrence).ShouldBe([Occurrence.MustNot, Occurrence.Filter]);
    }

    [Fact]
    public void CreateBoolQuery_NestedWithoutOccurrence_ChildrenAreAddedAsMust()
    {
        var outer = BuildNested(b => b.CreateBoolQuery(inner => inner
            .CreateTermQuery(t => t.WithField("City").WithValue("Warsaw"))));

        var inner = outer.NestedQueries.ShouldHaveSingleItem().ShouldBeOfType<BoolSearchSubQuery>();
        inner.Occurrence.ShouldBe(Occurrence.Must);
        inner.NestedQueries.ShouldHaveSingleItem().Occurrence.ShouldBe(Occurrence.Must);
    }

    [Fact]
    public void CreateBoolQuery_ShouldOnTheBool_BoolTakesParentOccurrenceAndChildrenShould()
    {
        var topLevel = Build(q => q.CreateBoolQuery(b => b.Should()
            .CreateTermQuery(t => t.WithField("City").WithValue("Warsaw"))));
        var nested = BuildNested(outer => outer.CreateBoolQuery(b => b.Should()
            .CreateTermQuery(t => t.WithField("City").WithValue("Warsaw"))));

        topLevel.Query.Select(clause => clause.Occurrence).Distinct().ShouldBe([Occurrence.Must]);
        var topLevelBool = topLevel.Query.Where(clause => clause.Occurrence == Occurrence.Must).ToList().ShouldHaveSingleItem().ShouldBeOfType<BoolSearchSubQuery>();
        topLevelBool.Occurrence.ShouldBe(Occurrence.Must);
        topLevelBool.NestedQueries.ShouldHaveSingleItem().Occurrence.ShouldBe(Occurrence.Should);
        var nestedBool = nested.NestedQueries.ShouldHaveSingleItem().ShouldBeOfType<BoolSearchSubQuery>();
        nestedBool.Occurrence.ShouldBe(Occurrence.Must);
        nestedBool.NestedQueries.ShouldHaveSingleItem().Occurrence.ShouldBe(Occurrence.Should);
    }

    [Fact]
    public void BooleanQueryConfigurator_New_OccurrenceIsMust() =>
        new BooleanQueryConfigurator().Occurrence.ShouldBe(Occurrence.Must);

    [Fact]
    public void CreateBoolQuery_TopLevelUnderShouldParent_BoolIsAddedAsShould()
    {
        var query = Build(q =>
        {
            q.Occurrence = Occurrence.Should;
            q.CreateBoolQuery(b => b.CreateTermQuery(t => t.WithField("City").WithValue("Warsaw")));
        });

        query.Query.Select(clause => clause.Occurrence).Distinct().ShouldBe([Occurrence.Should]);
        query.Query.Where(clause => clause.Occurrence == Occurrence.Should).ToList().ShouldHaveSingleItem().Occurrence.ShouldBe(Occurrence.Should);
    }

    [Fact]
    public void Query_OwnOccurrenceChosen_IsWrittenToTheQuery()
    {
        new FuzzyQueryConfigurator().WithField("City").WithValue("Warsaw").MustNot().Query.Occurrence.ShouldBe(Occurrence.MustNot);
        new PrefixQueryConfigurator().WithField("City").WithValue("War").Should().Query.Occurrence.ShouldBe(Occurrence.Should);
        new PrefixPhraseQueryConfigurator().WithField("City").WithValue("War").Filter().Query.Occurrence.ShouldBe(Occurrence.Filter);
        new TermQueryConfigurator().WithField("City").WithValue("Warsaw").MustNot().Query.Occurrence.ShouldBe(Occurrence.MustNot);
        new TermsQueryConfigurator().WithField("City").WithValue("Warsaw").Should().Query.Occurrence.ShouldBe(Occurrence.Should);
        new SpatialQueryConfigurator().WithField("Location").WithDistance(5).Filter().Query.Occurrence.ShouldBe(Occurrence.Filter);
        new NumericRangeQueryConfigurator().WithField("Age").WithMinValue(18).MustNot().Query.Occurrence.ShouldBe(Occurrence.MustNot);
        new LongRangeQueryConfigurator().WithField("Views").WithMinValue(18L).Should().Query.Occurrence.ShouldBe(Occurrence.Should);
        new StringRangeQueryConfigurator().WithField("Name").WithMinValue("a").Filter().Query.Occurrence.ShouldBe(Occurrence.Filter);
        new DateRangeQueryConfigurator().WithField("Born").WithMinValue(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)).MustNot()
            .Query.Occurrence.ShouldBe(Occurrence.MustNot);
    }

    // The parent bool is MUST; the first clause chose SHOULD and the second chose nothing.
    private static void AssertNested(Action<BooleanQueryConfigurator> configure)
    {
        var nested = BuildNested(b => configure(b.Must()));

        nested.NestedQueries.Select(q => q.Occurrence).ShouldBe([Occurrence.Should, Occurrence.Must]);
    }

    private static BoolSearchSubQuery BuildNested(Action<BooleanQueryConfigurator> configure)
    {
        var query = Build(q => q.CreateBoolQuery(configure));

        return query.Query.Where(clause => clause.Occurrence == Occurrence.Must).ToList().ShouldHaveSingleItem().ShouldBeOfType<BoolSearchSubQuery>();
    }

    private static void AssertTopLevel(ISearchQuery query, string shouldValue, string mustValue)
    {
        query.Query.Select(clause => clause.Occurrence).Distinct().ShouldBe([Occurrence.Should, Occurrence.Must], ignoreOrder: true);
        var should = query.Query.Where(clause => clause.Occurrence == Occurrence.Should).ToList().ShouldHaveSingleItem().ShouldBeOfType<TermSubQuery>();
        should.Value.ShouldBe(shouldValue);
        should.Occurrence.ShouldBe(Occurrence.Should);
        var must = query.Query.Where(clause => clause.Occurrence == Occurrence.Must).ToList().ShouldHaveSingleItem().ShouldBeOfType<TermSubQuery>();
        must.Value.ShouldBe(mustValue);
        must.Occurrence.ShouldBe(Occurrence.Must);
    }

    // The top-level query is MUST; the first clause chose SHOULD and the second chose nothing.
    private static void AssertTopLevel<T>(Action<IBaseQueryConfigurator> configure) where T : ISearchSubQuery
    {
        var query = Build(configure);

        query.Query.Select(clause => clause.Occurrence).Distinct().ShouldBe([Occurrence.Should, Occurrence.Must], ignoreOrder: true);
        query.Query.Where(clause => clause.Occurrence == Occurrence.Should).ToList().ShouldHaveSingleItem().ShouldBeOfType<T>().Occurrence.ShouldBe(Occurrence.Should);
        query.Query.Where(clause => clause.Occurrence == Occurrence.Must).ToList().ShouldHaveSingleItem().ShouldBeOfType<T>().Occurrence.ShouldBe(Occurrence.Must);
    }

    private static ISearchQuery Build(Action<IBaseQueryConfigurator> configure) =>
        FluentApiSearchQueryCreator.Create().CreateSearchQuery(configure);
}
