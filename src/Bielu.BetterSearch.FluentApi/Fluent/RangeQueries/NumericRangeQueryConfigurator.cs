using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent.RangeQueries
{
    public class NumericRangeQueryConfigurator
    {
        public NumericRange Query { get; set; } = new();

        public NumericRangeQueryConfigurator WithField(string fieldName)
        {
            Query.Field = fieldName;
            return this;
        }
        public NumericRangeQueryConfigurator WithMinValue(int value, bool includeEdge = false)
        {
            Query.MinValue = value;
            Query.IncludeMinEdge = includeEdge;
            return this;
        }
        public NumericRangeQueryConfigurator WithMaxValue(int value, bool includeEdge = false)
        {
            Query.MinValue = value;
            Query.IncludeMaxEdge = includeEdge;
            return this;
        }
    }
}
