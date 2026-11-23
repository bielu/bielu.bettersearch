using Bielu.BetterSearch.Abstractions.Query.Aggregations;

namespace Bielu.BetterSearch.FluentApi.Fluent.Aggregations
{
    /// <summary>Configures a <see cref="TermAggregation"/> facet.</summary>
    /// <param name="query">The facet to configure.</param>
    public class TermAggregationQueryConfigurator(TermAggregation query)
    {
        /// <summary>Sets the <see cref="TermAggregation.AggregationName"/> of the facet.</summary>
        /// <param name="name">The facet name.</param>
        /// <returns>This configurator, for chaining.</returns>
        public TermAggregationQueryConfigurator WithName(string name)
        {
            query.AggregationName = name;
            return this;
        }
        /// <summary>Sets the <see cref="TermAggregation.TermFieldName"/>, the field the facet groups by.</summary>
        /// <param name="name">The field name.</param>
        /// <returns>This configurator, for chaining.</returns>
        public TermAggregationQueryConfigurator WithField(string name)
        {
            query.TermFieldName = name;
            return this;
        }
        /// <summary>Sets the <see cref="TermAggregation.Size"/>, the number of buckets the facet returns.</summary>
        /// <param name="size">The number of buckets.</param>
        /// <returns>This configurator, for chaining.</returns>
        public TermAggregationQueryConfigurator WithSize(int size)
        {
            query.Size = size;
            return this;
        }
    }
}
