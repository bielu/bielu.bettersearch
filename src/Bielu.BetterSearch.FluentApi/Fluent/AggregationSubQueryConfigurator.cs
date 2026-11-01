using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.FluentApi.Fluent.Aggregations;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class AggregationSubQueryConfigurator(IAggregationQuery configuratorQuery)
    {
        public AggregationSubQueryConfigurator CreateFilterQuery(Action<FilterAggregationQueryConfigurator> query)
        {
            var configurator = new FilterAggregationQueryConfigurator(new FilterAggregationQuery());
            query.Invoke(configurator);
            configuratorQuery.NestedAggregations.Add(configurator.FilterAggregationQuery);
            return this;
        }


    }
}
