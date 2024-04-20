using Bielu.BetterSearch.Abstractions.Query.Aggregations;

namespace Bielu.BetterSearch.FluentApi.Fluent.Aggregations;

public class MultiTermAggregationQueryConfigurator
{
    public MultiTermAggreation Query;
    public MultiTermAggregationQueryConfigurator()
    {
        Query = new MultiTermAggreation();
    }



    public MultiTermAggregationQueryConfigurator WithName(string name)
    {
        Query.AggregationName = name;
        return this;
    }
    public MultiTermAggregationQueryConfigurator WithTerms(params SimpleTerm[] terms)
    {
        Query.Terms = terms.ToList();
        return this;
    }
    public MultiTermAggregationQueryConfigurator WithTerms(params string[] fieldsNames)
    {
        Query.Terms = fieldsNames.Select(x=>new SimpleTerm(){TermFieldName = x}).ToList();
        return this;
    }
}
