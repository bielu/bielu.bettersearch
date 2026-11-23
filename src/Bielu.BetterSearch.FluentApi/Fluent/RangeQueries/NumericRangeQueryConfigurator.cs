using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent.RangeQueries
{
    /// <summary>Configures a <see cref="NumericRangeSubQuery"/>.</summary>
    public class NumericRangeQueryConfigurator : SubQueryConfigurator<NumericRangeQueryConfigurator>
    {
        /// <summary>The range sub-query this configurator builds.</summary>
        public NumericRangeSubQuery Query { get; set; } = new();
        /// <inheritdoc />
        protected override ISearchSubQuery SubQuery => Query;

        /// <summary>Sets the <c>Field</c> the sub-query matches on.</summary>
        /// <param name="fieldName">The field name.</param>
        /// <returns>This configurator, for chaining.</returns>
        public NumericRangeQueryConfigurator WithField(string fieldName)
        {
            Query.Field = fieldName;
            return this;
        }
        /// <summary>Sets the <see cref="NumericRangeSubQuery.MinValue"/> and whether the range includes it (<c>IncludeMinEdge</c>).</summary>
        /// <param name="value">The lower bound.</param>
        /// <param name="includeEdge"><c>true</c> to include the bound in the range.</param>
        /// <returns>This configurator, for chaining.</returns>
        public NumericRangeQueryConfigurator WithMinValue(double value, bool includeEdge = true)
        {
            Query.MinValue = value;
            Query.IncludeMinEdge = includeEdge;
            return this;
        }
        /// <summary>Sets the <see cref="NumericRangeSubQuery.MaxValue"/> and whether the range includes it (<c>IncludeMaxEdge</c>).</summary>
        /// <param name="value">The upper bound.</param>
        /// <param name="includeEdge"><c>true</c> to include the bound in the range.</param>
        /// <returns>This configurator, for chaining.</returns>
        public NumericRangeQueryConfigurator WithMaxValue(double value, bool includeEdge = true)
        {
            Query.MaxValue = value;
            Query.IncludeMaxEdge = includeEdge;
            return this;
        }
    }
}
