using Bielu.BetterSearch.Abstractions.Results;

namespace Bielu.BetterSearch.FluentApi;

public class FluentApiSearchQueryCreator(ISearchQuery<IQueryResult> baseQuery) : IFluentApiSearchQueryCreator
{
    public FluentApiSearchQueryCreator WithSearchQuery(Action<QueryCreatorConfigurator> configurator)
    {
        var query = new QueryCreatorConfigurator(new SearchQuery());
        configurator.Invoke(query);
        baseQuery =   query.Query;
        return this;
    }
    public ISearchQuery<IQueryResult> CreateSearchQuery(Action<IBaseQueryConfigurator> configurator)
    {
        var newQuery = new FluentQueryConfigurator(baseQuery);
        newQuery.Occurance = Occurance.MUST;
        configurator.Invoke(newQuery);
        return newQuery.Query;
    }
    public static FluentApiSearchQueryCreator Create()
    {
        return new FluentApiSearchQueryCreator(new SearchQuery());
    }
}
