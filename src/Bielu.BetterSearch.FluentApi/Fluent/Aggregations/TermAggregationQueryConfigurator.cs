using Bielu.BetterSearch.Abstractions.Query.Aggregations;

namespace Bielu.BetterSearch.FluentApi.Fluent.Aggregations
{
    public class TermAggregationQueryConfigurator(TermAggregation query)
    {
        public TermAggregationQueryConfigurator WithName(string name)
        {
            query.AggregationName = name;
            return this;
        }
        public TermAggregationQueryConfigurator WithField(string name)
        {
            query.TermFieldName = name;
            return this;
        }
    }
}
