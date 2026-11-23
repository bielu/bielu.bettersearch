using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.FluentApi.Fluent.Aggregations;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    /// <summary>Adds nested facets to the <see cref="IAggregationQuery.NestedAggregations"/> of a facet.</summary>
    /// <param name="configuratorQuery">The facet the nested facets are added to.</param>
    public class AggregationSubQueryConfigurator(IAggregationQuery configuratorQuery)
    {
        /// <summary>Adds a nested <see cref="FilterAggregationQuery"/> facet that <paramref name="query"/> configures.</summary>
        /// <param name="query">Configures the filter facet.</param>
        /// <returns>This configurator, for chaining.</returns>
        public AggregationSubQueryConfigurator CreateFilterQuery(Action<FilterAggregationQueryConfigurator> query)
        {
            var configurator = new FilterAggregationQueryConfigurator(new FilterAggregationQuery());
            query.Invoke(configurator);
            configuratorQuery.NestedAggregations.Add(configurator.FilterAggregationQuery);
            return this;
        }


    }
}
