using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.FluentApi.Fluent;
using Bielu.BetterSearch.FluentApi.Fluent.Extensions;
using Shouldly;

namespace Bielu.BetterSearch.FluentApi.Tests;

/// <summary>Builds each part of the query model through the fluent API and checks the <see cref="ISearchQuery"/> it gives.</summary>
public sealed class FluentQueryBuilderTests
{
    [Fact]
    public void CreateGoeDistanceQuery_WithCenter_SetsLatAndLng()
    {
        var query = Build(q => q.CreateGoeDistanceQuery(s => s
            .WithField("Location").WithCenter(52.23, 21.01).WithDistance(5).WithUnitOfDistance("km")));

        var spatial = query.Query[Occurance.MUST].ShouldHaveSingleItem().ShouldBeOfType<SpatialSearchQuery>();
        spatial.Field.ShouldBe("Location");
        spatial.Lat.ShouldBe(52.23);
        spatial.Lng.ShouldBe(21.01);
        spatial.Distance.ShouldBe(5);
        spatial.UnitOfDistance.ShouldBe("km");
    }

    [Fact]
    public void CreateFuzzyQuery_AllOptions_SetsEachOption()
    {
        var query = Build(q => q.CreateFuzzyQuery(f => f
            .WithField("Name").WithValue("nowek").WithFuzziness(1, 2).WithPrefixLength(3).WithMaxExpansions(40)
            .WithTranspositions()));

        var fuzzy = query.Query[Occurance.MUST].ShouldHaveSingleItem().ShouldBeOfType<FuzzyQuery>();
        fuzzy.Fuziness.ShouldBe(Tuple.Create(1, 2));
        fuzzy.PrefixLenght.ShouldBe(3);
        fuzzy.MaxExpansions.ShouldBe(40);
        fuzzy.Transposition.ShouldBeTrue();
    }

    [Fact]
    public void CreateFuzzyQuery_SingleFuzziness_SetsBothBounds()
    {
        var fuzzy = new FuzzyQueryConfigurator().WithFuzziness(2).WithTranspositions(false).Query.ShouldBeOfType<FuzzyQuery>();

        fuzzy.Fuziness.ShouldBe(Tuple.Create(2, 2));
        fuzzy.Transposition.ShouldBeFalse();
    }

    [Fact]
    public void FuzzyQueryConfigurator_NegativeOption_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new FuzzyQueryConfigurator().WithFuzziness(-1)).ParamName.ShouldBe("low");
        Should.Throw<ArgumentOutOfRangeException>(() => new FuzzyQueryConfigurator().WithFuzziness(0, -1)).ParamName.ShouldBe("high");
        Should.Throw<ArgumentOutOfRangeException>(() => new FuzzyQueryConfigurator().WithPrefixLength(-1)).ParamName.ShouldBe("prefixLength");
        Should.Throw<ArgumentOutOfRangeException>(() => new FuzzyQueryConfigurator().WithMaxExpansions(-1)).ParamName.ShouldBe("maxExpansions");
    }

    [Fact]
    public void CreateLuceneQuery_NestedAndTopLevel_SetsTheLuceneQuery()
    {
        var query = Build(q => q
            .CreateLuceneQuery(l => l.WithQuery("City:Warsaw"))
            .CreateBoolQuery(b => b.CreateLuceneQuery(l => l.WithQuery("City:Krakow"))));

        var clauses = query.Query[Occurance.MUST];
        clauses.Count.ShouldBe(2);
        clauses[0].ShouldBeOfType<LuceneSubQuery>().LuceneQuery.ShouldBe("City:Warsaw");
        var nested = clauses[1].ShouldBeOfType<BoolSearchSubQuery>().NestedQueries.ShouldHaveSingleItem();
        nested.ShouldBeOfType<LuceneSubQuery>().LuceneQuery.ShouldBe("City:Krakow");
    }

    [Fact]
    public void WithBoost_SubQueryAndBool_SetsBoostValue()
    {
        var query = Build(q => q
            .CreateTermQuery(t => t.WithField("City").WithValue("Warsaw").WithBoost(3))
            .CreateBoolQuery(b => b.WithBoost(5).CreatePrefixQuery(p => p.WithField("City").WithValue("Kra").WithBoost(7))));

        var clauses = query.Query[Occurance.MUST];
        clauses[0].ShouldBeOfType<TermSubQuery>().BoostValue.ShouldBe(3);
        var boolQuery = clauses[1].ShouldBeOfType<BoolSearchSubQuery>();
        boolQuery.BoostValue.ShouldBe(5);
        boolQuery.NestedQueries.ShouldHaveSingleItem().ShouldBeOfType<PrefixSubQuery>().BoostValue.ShouldBe(7);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void WithBoost_ZeroOrNegative_Throws(int boost)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new TermQueryConfigurator().WithBoost(boost)).ParamName.ShouldBe("boost");
        Should.Throw<ArgumentOutOfRangeException>(() => new BooleanQueryConfigurator().WithBoost(boost)).ParamName.ShouldBe("boost");
    }

    [Fact]
    public void SortBy_TwoFields_AddsThemInOrder()
    {
        var query = FluentApiSearchQueryCreator.Create()
            .WithSearchQuery(s => s.SortByDescending("Born").SortBy("Name"))
            .CreateSearchQuery(_ => { });

        query.SortOrder.Select(sort => (sort.FieldName, sort.Desc, sort.Order)).ShouldBe([("Born", true, 0), ("Name", false, 1)]);
    }

    [Fact]
    public void WithProperties_CalledTwice_KeepsTheLast()
    {
        var query = FluentApiSearchQueryCreator.Create()
            .WithSearchQuery(s => s.WithProperties("Name", "City").WithProperties("Title"))
            .CreateSearchQuery(_ => { });

        query.Properties.ShouldBe(["Title"]);
    }

    [Fact]
    public void WithSearchQuery_QueryPassedToConstructor_IsKept()
    {
        var baseQuery = new SearchQuery { Index = "pages" };

        var query = new FluentApiSearchQueryCreator(baseQuery)
            .WithSearchQuery(s => s.WithPage(2))
            .CreateSearchQuery(q => q.CreateTermQuery(t => t.WithField("City").WithValue("Warsaw")));

        query.ShouldBeSameAs(baseQuery);
        query.Index.ShouldBe("pages");
        query.Page.ShouldBe(2);
        query.Query[Occurance.MUST].ShouldHaveSingleItem().ShouldBeOfType<TermSubQuery>().Value.ShouldBe("Warsaw");
    }

    [Fact]
    public void CreateAggregationQuery_TermAndMultiTermWithSize_SetsTheSize()
    {
        var query = Build(q => q.CreateAggregationQuery(a => a
            .CreateTermQuery(t => t.WithName("cities").WithField("City").WithSize(25))
            .CreateMultiTermQuery(m => m.WithName("pairs").WithTerms("City", "Type").WithSize(3))));

        query.FacetQueries.Count.ShouldBe(2);
        var term = query.FacetQueries[0].ShouldBeOfType<TermAggregation>();
        (term.AggregationName, term.TermFieldName, term.Size).ShouldBe(("cities", "City", 25));
        var multiTerm = query.FacetQueries[1].ShouldBeOfType<MultiTermAggreation>();
        multiTerm.Size.ShouldBe(3);
        multiTerm.Terms.Select(t => t.TermFieldName).ShouldBe(["City", "Type"]);
    }

    [Fact]
    public void CreatePostFilterQuery_TwoFilters_AddsBothToThePostFilterOnly()
    {
        var query = Build(q => q
            .CreateTermQuery(t => t.WithField("Type").WithValue("person"))
            .CreatePostFilterQuery(b => b.CreateTermQuery(t => t.WithField("City").WithValue("Warsaw")))
            .CreatePostFilterQuery(b => b.Should()
                .CreateTermQuery(t => t.WithField("City").WithValue("Krakow"))
                .CreateTermQuery(t => t.WithField("City").WithValue("Gdansk"))));

        query.Query[Occurance.MUST].ShouldHaveSingleItem().ShouldBeOfType<TermSubQuery>().Value.ShouldBe("person");
        query.PostFilterQuery.Keys.ShouldBe([Occurance.MUST]);
        var filters = query.PostFilterQuery[Occurance.MUST];
        filters.Count.ShouldBe(2);
        filters.ShouldAllBe(filter => filter.Occurance == Occurance.MUST);
        filters[0].NestedQueries.ShouldHaveSingleItem().ShouldBeOfType<TermSubQuery>().Value.ShouldBe("Warsaw");
        filters[1].NestedQueries.Select(clause => (clause.Occurance, ((TermSubQuery)clause).Value))
            .ShouldBe([(Occurance.SHOULD, (object?)"Krakow"), (Occurance.SHOULD, "Gdansk")]);
    }

    [Fact]
    public void CreatePostFilterQuery_UnderShouldParent_IsAddedAsShould()
    {
        var query = Build(q =>
        {
            q.Occurance = Occurance.SHOULD;
            q.CreatePostFilterQuery(b => b.CreateTermQuery(t => t.WithField("City").WithValue("Warsaw")));
        });

        query.PostFilterQuery.Keys.ShouldBe([Occurance.SHOULD]);
        var filter = query.PostFilterQuery[Occurance.SHOULD].ShouldHaveSingleItem();
        filter.Occurance.ShouldBe(Occurance.SHOULD);
        filter.NestedQueries.ShouldHaveSingleItem().Occurance.ShouldBe(Occurance.SHOULD);
    }

    [Fact]
    public void CreatePostFilterQuery_PostFilterQueryIsNull_CreatesIt()
    {
        var query = new FluentApiSearchQueryCreator(new SearchQuery { PostFilterQuery = null! })
            .CreateSearchQuery(q => q.CreatePostFilterQuery(b => b.CreateTermQuery(t => t.WithField("City").WithValue("Warsaw"))));

        query.PostFilterQuery[Occurance.MUST].ShouldHaveSingleItem();
    }

    [Fact]
    public void CreateHighlighterQuery_FieldAndAllFields_AddsOneRequestEach()
    {
        var query = Build(q => q
            .CreateHighlighterQuery(h => h.WithField("Title"))
            .CreateHighlighterQuery(h => h.WithField("Title").ForAllFields()));

        query.HighlightQueries.Count.ShouldBe(2);
        query.HighlightQueries[0].Field.ShouldBe("Title");
        query.HighlightQueries[0].IsAllField.ShouldBeFalse();
        query.HighlightQueries[0].Queries.ShouldBeEmpty();
        query.HighlightQueries[1].IsAllField.ShouldBeTrue();
    }

    [Fact]
    public void CreateHighlighterQuery_WithQuery_SetsTheOwnQueryUnderMust()
    {
        var query = Build(q => q.CreateHighlighterQuery(h => h
            .WithField("Title")
            .WithQuery(b => b.CreateTermQuery(t => t.WithField("Title").WithValue("old")))
            .WithQuery(b => b.CreateTermQuery(t => t.WithField("Title").WithValue("welcome")))));

        var request = query.HighlightQueries.ShouldHaveSingleItem();
        request.Queries.Keys.ShouldBe([Occurance.MUST]);
        var own = request.Queries[Occurance.MUST].ShouldBeOfType<BoolSearchSubQuery>();
        own.NestedQueries.ShouldHaveSingleItem().ShouldBeOfType<TermSubQuery>().Value.ShouldBe("welcome");
    }

    private static ISearchQuery Build(Action<IBaseQueryConfigurator> configure) =>
        FluentApiSearchQueryCreator.Create().CreateSearchQuery(configure);
}
