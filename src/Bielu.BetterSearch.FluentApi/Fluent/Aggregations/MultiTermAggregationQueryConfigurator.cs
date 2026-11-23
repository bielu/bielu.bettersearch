using Bielu.BetterSearch.Abstractions.Query.Aggregations;

namespace Bielu.BetterSearch.FluentApi.Fluent.Aggregations;

/// <summary>Configures a <see cref="MultiTermAggregation"/> facet.</summary>
/// <param name="query">The facet to configure.</param>
public class MultiTermAggregationQueryConfigurator(MultiTermAggregation query)
{
    /// <summary>Sets the <see cref="MultiTermAggregation.AggregationName"/> of the facet.</summary>
    /// <param name="name">The facet name.</param>
    /// <returns>This configurator, for chaining.</returns>
    public MultiTermAggregationQueryConfigurator WithName(string name)
    {
        query.AggregationName = name;
        return this;
    }
    /// <summary>Sets the <see cref="MultiTermAggregation.Terms"/> of the facet, replacing any set before.</summary>
    /// <param name="terms">The terms to group by.</param>
    /// <returns>This configurator, for chaining.</returns>
    public MultiTermAggregationQueryConfigurator WithTerms(params SimpleTerm[] terms)
    {
        query.Terms = terms.ToList();
        return this;
    }
    /// <summary>Sets the <see cref="MultiTermAggregation.Terms"/> of the facet to one <see cref="SimpleTerm"/> per field, replacing any set before.</summary>
    /// <param name="fieldsNames">The names of the fields to group by.</param>
    /// <returns>This configurator, for chaining.</returns>
    public MultiTermAggregationQueryConfigurator WithTerms(params string[] fieldsNames)
    {
        query.Terms = fieldsNames.Select(x => new SimpleTerm() { TermFieldName = x }).ToList();
        return this;
    }
    /// <summary>Sets the <see cref="MultiTermAggregation.Size"/>, the number of buckets the facet returns.</summary>
    /// <param name="size">The number of buckets.</param>
    /// <returns>This configurator, for chaining.</returns>
    public MultiTermAggregationQueryConfigurator WithSize(int size)
    {
        query.Size = size;
        return this;
    }
}
