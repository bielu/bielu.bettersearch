using Bielu.BetterSearch.Abstractions.Query.Aggregations;

namespace Bielu.BetterSearch.FluentApi.Fluent.Extensions
{
    /// <summary>Extension methods that add facets to a query configurator.</summary>
    public static class CreateAggregationQueryHelper
    {
        //todo: better dsl coverage
        /// <summary>Creates a facet of type <typeparamref name="T"/>, configures it with <paramref name="query"/> and adds it to <see cref="ISearchQuery.FacetQueries"/>.</summary>
        /// <typeparam name="T">The facet type.</typeparam>
        /// <param name="configurator">The configurator whose query gets the facet.</param>
        /// <param name="query">Configures the facet.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IBaseQueryConfigurator CreateAggregationQuery<T>(this IBaseQueryConfigurator configurator,
            Action<T> query) where T : IAggregationQuery, new()
        {
            var booleanQuery = new T();
            query.Invoke(booleanQuery);
            configurator.Query.FacetQueries.Add(booleanQuery);
            return configurator;
        }
        /// <summary>Adds the facets that <paramref name="query"/> creates to <see cref="ISearchQuery.FacetQueries"/>.</summary>
        /// <param name="configurator">The configurator whose query gets the facets.</param>
        /// <param name="query">Creates the facets.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IBaseQueryConfigurator CreateAggregationQuery(this IBaseQueryConfigurator configurator,
            Action<AggregationQueryConfigurator> query)
        {
            var booleanQuery = new AggregationQueryConfigurator(configurator.Query);
            query.Invoke(booleanQuery);

            return configurator;
        }
        /// <summary>Creates a facet of type <typeparamref name="T"/>, configures it with <paramref name="query"/> and nests it in the filter facet.</summary>
        /// <typeparam name="T">The facet type.</typeparam>
        /// <param name="configurator">The configurator of the filter facet that gets the nested facet.</param>
        /// <param name="query">Configures the nested facet.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IAggregationConfigurator CreateSubAggregationQuery<T>(this IAggregationConfigurator configurator,
            Action<T> query) where T : IAggregationQuery, new()
        {
            var booleanQuery = new T();
            query.Invoke(booleanQuery);
            configurator.FilterAggregationQuery.NestedAggregations.Add(booleanQuery);
            return configurator;
        }
        /// <summary>Nests the facets that <paramref name="query"/> creates in the filter facet.</summary>
        /// <param name="configurator">The configurator of the filter facet that gets the nested facets.</param>
        /// <param name="query">Creates the nested facets.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IAggregationConfigurator CreateSubAggregationQuery(this IAggregationConfigurator configurator,
            Action<AggregationSubQueryConfigurator> query)
        {
            var booleanQuery = new AggregationSubQueryConfigurator(configurator.FilterAggregationQuery);
            query.Invoke(booleanQuery);

            return configurator;
        }
    }
}
