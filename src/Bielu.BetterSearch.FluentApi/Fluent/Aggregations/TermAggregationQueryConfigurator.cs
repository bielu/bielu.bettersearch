using Bielu.BetterSearch.Abstractions.Query.Aggregations;

namespace Bielu.BetterSearch.FluentApi.Fluent.Aggregations
{
    public class TermAggregationQueryConfigurator
    {
        public TermAggregation Query;
        public TermAggregationQueryConfigurator()
        {
            Query = new TermAggregation();
        }



        public TermAggregationQueryConfigurator WithName(string name)
        {
            Query.AggregationName = name;
            return this;
        }
        public TermAggregationQueryConfigurator WithField(string name)
        {
            Query.TermFieldName = name;
            return this;
        }
    }
}
