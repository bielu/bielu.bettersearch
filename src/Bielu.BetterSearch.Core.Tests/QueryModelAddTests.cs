using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Query.Highlighter;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Shouldly;

namespace Bielu.BetterSearch.Core.Tests;

public class QueryModelAddTests
{
    public static TheoryData<string> Containers() =>
        [nameof(SearchQuery), nameof(BoolSearchSubQuery), nameof(FilterAggregationQuery), nameof(BaseHighlightQuery)];

    private static ICreatableSearchQuery Create(string container) => container switch
    {
        nameof(SearchQuery) => new SearchQuery { Index = "index" },
        nameof(BoolSearchSubQuery) => new BoolSearchSubQuery(),
        nameof(FilterAggregationQuery) => new FilterAggregationQuery { AggregationName = "facet" },
        nameof(BaseHighlightQuery) => new BaseHighlightQuery(),
        _ => throw new ArgumentOutOfRangeException(nameof(container))
    };

    private static IList<ISearchSubQuery> Clauses(ICreatableSearchQuery container) => container switch
    {
        SearchQuery query => query.Query,
        BoolSearchSubQuery boolQuery => boolQuery.NestedQueries,
        FilterAggregationQuery filter => filter.Queries,
        BaseHighlightQuery highlight => highlight.Queries,
        _ => throw new ArgumentOutOfRangeException(nameof(container))
    };

    [Theory]
    [MemberData(nameof(Containers))]
    public void Add_NullClause_ThrowsArgumentNullException(string container)
    {
        var query = Create(container);
        var exception = Should.Throw<ArgumentNullException>(() => query.Add(Occurrence.Should, null!), container);

        exception.ParamName.ShouldBe("value");
        Clauses(query).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(Containers))]
    public void Add_Clause_AppendsItWithTheGivenOccurrence(string container)
    {
        var query = Create(container);
        var first = new TermSubQuery { Field = "Name", Value = "jane" };
        var second = new TermSubQuery { Field = "Name", Value = "john", Occurrence = Occurrence.MustNot };

        query.Add(Occurrence.Should, first);
        query.Add(Occurrence.Filter, second);

        Clauses(query).ShouldBe([first, second], container);
        first.Occurrence.ShouldBe(Occurrence.Should);
        second.Occurrence.ShouldBe(Occurrence.Filter);
    }
}
