using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent.RangeQueries
{
    public class NumericRangeQueryConfigurator : SubQueryConfigurator<NumericRangeQueryConfigurator>
    {
        public NumericRange Query { get; set; } = new();
        protected override ISearchSubQuery SubQuery => Query;

        public NumericRangeQueryConfigurator WithField(string fieldName)
        {
            Query.Field = fieldName;
            return this;
        }
        public NumericRangeQueryConfigurator WithMinValue(double value, bool includeEdge = true)
        {
            Query.MinValue = value;
            Query.IncludeMinEdge = includeEdge;
            return this;
        }
        public NumericRangeQueryConfigurator WithMaxValue(double value, bool includeEdge = true)
        {
            Query.MaxValue = value;
            Query.IncludeMaxEdge = includeEdge;
            return this;
        }
    }
}
