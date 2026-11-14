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
    public DateRangeQueryConfigurator WithMinValue(DateTime value, bool includeEdge = false)
    {
        Query.MinValue = value;
        Query.IncludeMinEdge = includeEdge;
        return this;
    }
    public DateRangeQueryConfigurator WithMaxValue(DateTime value, bool includeEdge = false)
    {
        Query.MinValue = value;
        Query.IncludeMaxEdge = includeEdge;
        return this;
    }
}
