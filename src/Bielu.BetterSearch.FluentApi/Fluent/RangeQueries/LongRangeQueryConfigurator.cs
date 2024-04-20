using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent.RangeQueries;

public class LongRangeQueryConfigurator
{
    public LongRangeQueryConfigurator()
    {
        Query = new LongRange();
    }

    public LongRange Query { get; set; }
    public LongRangeQueryConfigurator WithField(string fieldName)
    {
        Query.Field = fieldName;
        return this;
    }
    public LongRangeQueryConfigurator WithMinValue(long value, bool includeEdge = false)
    {
        Query.MinValue = value;
        Query.IncludeMinEdge = includeEdge;
        return this;
    }
    public LongRangeQueryConfigurator WithMaxValue(long value, bool includeEdge = false)
    {
        Query.MinValue = value;
        Query.IncludeMaxEdge = includeEdge;
        return this;
    }
}
