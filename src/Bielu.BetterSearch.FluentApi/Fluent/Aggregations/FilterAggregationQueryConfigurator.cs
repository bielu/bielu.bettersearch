using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent.Aggregations
{
    /// <summary>Configures a <see cref="Abstractions.Query.Aggregations.FilterAggregationQuery"/>: sub-queries created on it are added to a bool query that the facet filters on.</summary>
    public class FilterAggregationQueryConfigurator : IAggregationConfigurator
    {
        /// <inheritdoc />
        public FilterAggregationQuery FilterAggregationQuery { get; }

        /// <summary>Creates a configurator for <paramref name="filterAggregationQuery"/> and adds a new bool query to it as a <c>Must</c> clause.</summary>
        /// <param name="filterAggregationQuery">The filter facet to configure.</param>
        public FilterAggregationQueryConfigurator(FilterAggregationQuery filterAggregationQuery)
        {
            FilterAggregationQuery = filterAggregationQuery;
            Query = new BoolSearchSubQuery();
            FilterAggregationQuery.Add(Occurrence.Must, Query);
        }


        /// <inheritdoc />
        public INestableQuery Query { get; set; }
        /// <inheritdoc />
        public Occurrence Occurrence { get; set; }
    }
}
