using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.FluentApi.Fluent.Aggregations;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class AggregationQueryConfigurator(ISearchQuery<ISearchResult<ISearchModel>> configuratorQuery)
    {
        public AggregationQueryConfigurator CreateFilterQuery(Action<FilterAggregationQueryConfigurator> query)
        {
            var configurator = new FilterAggregationQueryConfigurator(new FilterAggregationQuery());
            query.Invoke(configurator);
            configuratorQuery.FacetQueries.Add(configurator.FilterAggregationQuery);
            return this;
        }
        public AggregationQueryConfigurator CreateTermQuery(Action<TermAggregationQueryConfigurator> query)
        {
            var termAggregation = new TermAggregation();
            var configurator = new TermAggregationQueryConfigurator(termAggregation);
            query.Invoke(configurator);
            configuratorQuery.FacetQueries.Add(termAggregation);
            return this;
        }
        public AggregationQueryConfigurator CreateMultiTermQuery(Action<MultiTermAggregationQueryConfigurator> query)
        {
            var termAggregation = new MultiTermAggreation();
            var configurator = new MultiTermAggregationQueryConfigurator(termAggregation);
            query.Invoke(configurator);
            configuratorQuery.FacetQueries.Add(termAggregation);
            return this;
        }

    }
}
