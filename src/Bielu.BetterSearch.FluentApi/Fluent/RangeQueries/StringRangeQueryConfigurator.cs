using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent.RangeQueries;

public class StringRangeQueryConfigurator
{
    public StringRangeQueryConfigurator()
    {
        Query = new StringRange();
    }

    public StringRange Query { get; set; }
    public StringRangeQueryConfigurator WithField(string fieldName)
    {
        Query.Field = fieldName;
        return this;
    }
    public StringRangeQueryConfigurator WithMinValue(string value, bool includeEdge = false)
    {
        Query.MinValue = value;
        Query.IncludeMinEdge = includeEdge;
        return this;
    }
    public StringRangeQueryConfigurator WithMaxValue(string value, bool includeEdge = false)
    {
        Query.MinValue = value;
        Query.IncludeMaxEdge = includeEdge;
        return this;
    }
}
