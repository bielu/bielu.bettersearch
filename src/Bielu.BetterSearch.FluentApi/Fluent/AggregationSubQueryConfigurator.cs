using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.FluentApi.Fluent.Aggregations;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class AggregationSubQueryConfigurator
    {
        private readonly IAggregationQuery _configuratorQuery;

        public AggregationSubQueryConfigurator(IAggregationQuery configuratorQuery)
        {
            _configuratorQuery = configuratorQuery;
        }

        public AggregationSubQueryConfigurator CreateFilterQuery(Action<FilterAggregationQueryConfigurator> query)
        {
            var configurator = new FilterAggregationQueryConfigurator(new FilterAggregationQuery());
            query.Invoke(configurator);
            _configuratorQuery.NestedAggregations.Add(configurator.FilterAggregationQuery);
            return this;
        }


    }
}
