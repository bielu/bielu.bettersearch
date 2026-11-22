using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent.Aggregations
{
    public class FilterAggregationQueryConfigurator : IAggregationConfigurator
    {
        public FilterAggregationQuery FilterAggregationQuery { get; }

        public FilterAggregationQueryConfigurator(FilterAggregationQuery filterAggregationQuery)
        {
            FilterAggregationQuery = filterAggregationQuery;
            Query = new BoolSearchSubQuery();
            FilterAggregationQuery.Add(Occurrence.Must, Query);
        }


        public INestableQuery Query { get; set; }
        public Occurrence Occurrence { get; set; }
    }
}
