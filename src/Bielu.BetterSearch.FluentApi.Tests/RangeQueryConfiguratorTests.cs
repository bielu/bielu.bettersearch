using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.FluentApi.Fluent;
using Bielu.BetterSearch.FluentApi.Fluent.Extensions;
using Bielu.BetterSearch.FluentApi.Fluent.RangeQueries;
using Shouldly;

namespace Bielu.BetterSearch.FluentApi.Tests;

/// <summary>Builds each range query through the fluent API and checks both bounds and both edge flags.</summary>
public sealed class RangeQueryConfiguratorTests
{
    private static T BuildRange<T>(Action<IQueryConfigurator> range) where T : BaseRangeQuery
    {
        var query = (SearchQuery)FluentApiSearchQueryCreator.Create()
            .CreateSearchQuery(search => search.CreateBoolQuery(boolQuery => range(boolQuery)));

        var boolQuery = query.Query[Occurance.MUST].ShouldHaveSingleItem().ShouldBeOfType<BoolSearchSubQuery>();
        return boolQuery.NestedQueries.ShouldHaveSingleItem().ShouldBeOfType<T>();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void CreateNumericRangeQuery_MinAndMaxValue_SetsBothBoundsAndEdges(bool includeMin, bool includeMax)
    {
        var range = BuildRange<NumericRange>(query => query.CreateNumericRangeQuery(r => r
            .WithField("Age").WithMinValue(1, includeMin).WithMaxValue(10, includeMax)));

        range.Field.ShouldBe("Age");
        range.MinValue.ShouldBe(1);
        range.MaxValue.ShouldBe(10);
        range.IncludeMinEdge.ShouldBe(includeMin);
        range.IncludeMaxEdge.ShouldBe(includeMax);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void CreateLongRangeQuery_MinAndMaxValue_SetsBothBoundsAndEdges(bool includeMin, bool includeMax)
    {
        var range = BuildRange<LongRange>(query => query.CreateLongRangeQuery(r => r
            .WithField("Views").WithMinValue(1L, includeMin).WithMaxValue(9_007_199_254_740_993L, includeMax)));

        range.Field.ShouldBe("Views");
        range.MinValue.ShouldBe(1L);
        range.MaxValue.ShouldBe(9_007_199_254_740_993L);
        range.IncludeMinEdge.ShouldBe(includeMin);
        range.IncludeMaxEdge.ShouldBe(includeMax);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void CreateStringRangeQuery_MinAndMaxValue_SetsBothBoundsAndEdges(bool includeMin, bool includeMax)
    {
        var range = BuildRange<StringRange>(query => query.CreateStringRangeQuery(r => r
            .WithField("Name").WithMinValue("a", includeMin).WithMaxValue("m", includeMax)));

        range.Field.ShouldBe("Name");
        range.MinValue.ShouldBe("a");
        range.MaxValue.ShouldBe("m");
        range.IncludeMinEdge.ShouldBe(includeMin);
        range.IncludeMaxEdge.ShouldBe(includeMax);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void CreateDateRangeQuery_MinAndMaxValue_SetsBothBoundsAndEdges(bool includeMin, bool includeMax)
    {
        var min = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var max = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        var range = BuildRange<DateRangeQuery>(query => query.CreateDateRangeQuery(r => r
            .WithField("Born").WithMinValue(min, includeMin).WithMaxValue(max, includeMax)));

        range.Field.ShouldBe("Born");
        range.MinValue.ShouldBe(min);
        range.MaxValue.ShouldBe(max);
        range.IncludeMinEdge.ShouldBe(includeMin);
        range.IncludeMaxEdge.ShouldBe(includeMax);
    }

    [Fact]
    public void WithMaxValue_WithoutMinValue_LeavesMinValueUnset()
    {
        var numeric = new NumericRangeQueryConfigurator().WithMaxValue(10).Query;
        var longRange = new LongRangeQueryConfigurator().WithMaxValue(10L).Query;
        var stringRange = new StringRangeQueryConfigurator().WithMaxValue("m").Query;
        var dateRange = new DateRangeQueryConfigurator().WithMaxValue(new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc)).Query;

        numeric.MinValue.ShouldBeNull();
        numeric.MaxValue.ShouldBe(10);
        longRange.MinValue.ShouldBeNull();
        longRange.MaxValue.ShouldBe(10L);
        stringRange.MinValue.ShouldBeNull();
        stringRange.MaxValue.ShouldBe("m");
        dateRange.MinValue.ShouldBeNull();
        dateRange.MaxValue.ShouldBe(new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc));
    }
}
