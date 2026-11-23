using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.FluentApi.Fluent.Aggregations;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    /// <summary>Adds facets to the <see cref="ISearchQuery.FacetQueries"/> of a search query.</summary>
    /// <param name="configuratorQuery">The search query the facets are added to.</param>
    public class AggregationQueryConfigurator(ISearchQuery configuratorQuery)
    {
        /// <summary>Adds a <see cref="FilterAggregationQuery"/> facet that <paramref name="query"/> configures.</summary>
        /// <param name="query">Configures the filter facet.</param>
        /// <returns>This configurator, for chaining.</returns>
        public AggregationQueryConfigurator CreateFilterQuery(Action<FilterAggregationQueryConfigurator> query)
        {
            var configurator = new FilterAggregationQueryConfigurator(new FilterAggregationQuery { AggregationName = string.Empty });
            query.Invoke(configurator);
            configuratorQuery.FacetQueries.Add(configurator.FilterAggregationQuery);
            return this;
        }
        /// <summary>Adds a <see cref="TermAggregation"/> facet that <paramref name="query"/> configures.</summary>
        /// <param name="query">Configures the term facet.</param>
        /// <returns>This configurator, for chaining.</returns>
        public AggregationQueryConfigurator CreateTermQuery(Action<TermAggregationQueryConfigurator> query)
        {
            var termAggregation = new TermAggregation { AggregationName = string.Empty, TermFieldName = string.Empty };
            var configurator = new TermAggregationQueryConfigurator(termAggregation);
            query.Invoke(configurator);
            configuratorQuery.FacetQueries.Add(termAggregation);
            return this;
        }
        /// <summary>Adds a <see cref="MultiTermAggregation"/> facet that <paramref name="query"/> configures.</summary>
        /// <param name="query">Configures the multi-term facet.</param>
        /// <returns>This configurator, for chaining.</returns>
        public AggregationQueryConfigurator CreateMultiTermQuery(Action<MultiTermAggregationQueryConfigurator> query)
        {
            var termAggregation = new MultiTermAggregation { AggregationName = string.Empty };
            var configurator = new MultiTermAggregationQueryConfigurator(termAggregation);
            query.Invoke(configurator);
            configuratorQuery.FacetQueries.Add(termAggregation);
            return this;
        }

    }
}
