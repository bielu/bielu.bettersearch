
using Bielu.BetterSearch.Abstractions.Query.Aggregations;

namespace Bielu.BetterSearch.FluentApi.Fluent;

public interface IAggregationConfigurator : IQueryConfigurator
{
    public FilterAggregationQuery FilterAggregationQuery { get; }
}
