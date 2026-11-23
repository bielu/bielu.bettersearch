
using Bielu.BetterSearch.Abstractions.Query.Aggregations;

namespace Bielu.BetterSearch.FluentApi.Fluent;

/// <summary>A query configurator for a filter facet, which can also nest facets in it.</summary>
public interface IAggregationConfigurator : IQueryConfigurator
{
    /// <summary>The filter facet this configurator builds.</summary>
    public FilterAggregationQuery FilterAggregationQuery { get; }
}
