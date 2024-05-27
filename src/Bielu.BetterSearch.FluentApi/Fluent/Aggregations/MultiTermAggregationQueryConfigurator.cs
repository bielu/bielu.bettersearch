using Bielu.BetterSearch.Abstractions.Query.Aggregations;

namespace Bielu.BetterSearch.FluentApi.Fluent.Aggregations;

public class MultiTermAggregationQueryConfigurator
{
    private MultiTermAggreation _query;
    public MultiTermAggregationQueryConfigurator(MultiTermAggreation query)
    {
        _query = query;
    }



    public MultiTermAggregationQueryConfigurator WithName(string name)
    {
        _query.AggregationName = name;
        return this;
    }
    public MultiTermAggregationQueryConfigurator WithTerms(params SimpleTerm[] terms)
    {
        _query.Terms = terms.ToList();
        return this;
    }
    public MultiTermAggregationQueryConfigurator WithTerms(params string[] fieldsNames)
    {
        _query.Terms = fieldsNames.Select(x=>new SimpleTerm(){TermFieldName = x}).ToList();
        return this;
    }
}
