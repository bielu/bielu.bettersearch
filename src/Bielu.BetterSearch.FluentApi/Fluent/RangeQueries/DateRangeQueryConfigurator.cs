using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent.RangeQueries;

public class DateRangeQueryConfigurator : SubQueryConfigurator<DateRangeQueryConfigurator>
{
    public DateRangeQueryConfigurator()
    {
        Query = new DateRangeQuery();
    }

    public DateRangeQuery Query { get; set; }
    protected override ISearchSubQuery SubQuery => Query;
    public DateRangeQueryConfigurator WithField(string fieldName)
    {
        Query.Field = fieldName;
        return this;
    }
    /// <summary>The minimum: a <see cref="DateTime"/> or a <see cref="DateTimeOffset"/>; an unspecified <see cref="DateTime"/> is UTC.</summary>
    public DateRangeQueryConfigurator WithMinValue(DateRangeValue value, bool includeEdge = true)
    {
        Query.MinValue = value;
        Query.IncludeMinEdge = includeEdge;
        return this;
    }
    /// <summary>The maximum: a <see cref="DateTime"/> or a <see cref="DateTimeOffset"/>; an unspecified <see cref="DateTime"/> is UTC.</summary>
    public DateRangeQueryConfigurator WithMaxValue(DateRangeValue value, bool includeEdge = true)
    {
        Query.MaxValue = value;
        Query.IncludeMaxEdge = includeEdge;
        return this;
    }
}
