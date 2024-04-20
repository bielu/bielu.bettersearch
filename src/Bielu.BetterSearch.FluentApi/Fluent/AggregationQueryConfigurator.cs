using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.FluentApi.Fluent.Aggregations;
using SImpl.SearchModule.Abstraction.Results;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class AggregationQueryConfigurator
    {
        private readonly ISearchQuery<IQueryResult> _configuratorQuery;

        public AggregationQueryConfigurator(ISearchQuery<IQueryResult> configuratorQuery)
        {
            _configuratorQuery = configuratorQuery;
        }

        public AggregationQueryConfigurator CreateFilterQuery(Action<FilterAggregationQueryConfigurator> query)
        {
            var configurator = new FilterAggregationQueryConfigurator(new FilterAggregationQuery());
            query.Invoke(configurator);
            _configuratorQuery.FacetQueries.Add(configurator.FilterAggregationQuery);
            return this;
        }
        public AggregationQueryConfigurator CreateTermQuery(Action<TermAggregationQueryConfigurator> query)
        {
            var configurator = new TermAggregationQueryConfigurator();
            query.Invoke(configurator);
            _configuratorQuery.FacetQueries.Add(configurator.Query);
            return this;
        }
        public AggregationQueryConfigurator CreateMultiTermQuery(Action<TermAggregationQueryConfigurator> query)
        {
            var configurator = new TermAggregationQueryConfigurator();
            query.Invoke(configurator);
            _configuratorQuery.FacetQueries.Add(configurator.Query);
            return this;
        }

    }
}
