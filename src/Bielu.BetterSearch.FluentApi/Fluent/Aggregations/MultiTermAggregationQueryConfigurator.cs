using Bielu.BetterSearch.Abstractions.Query.Aggregations;

namespace Bielu.BetterSearch.FluentApi.Fluent.Aggregations;

public class MultiTermAggregationQueryConfigurator(MultiTermAggreation query)
{
    public MultiTermAggregationQueryConfigurator WithName(string name)
    {
        query.AggregationName = name;
        return this;
    }
    public MultiTermAggregationQueryConfigurator WithTerms(params SimpleTerm[] terms)
    {
        query.Terms = terms.ToList();
        return this;
    }
    public MultiTermAggregationQueryConfigurator WithTerms(params string[] fieldsNames)
    {
        query.Terms = fieldsNames.Select(x => new SimpleTerm() { TermFieldName = x }).ToList();
        return this;
    }
}
