
using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.FluentApi;

public class FluentApiSearchQueryCreator(ISearchQuery<ISearchResult<ISearchModel>> baseQuery) : IFluentApiSearchQueryCreator
{
    public FluentApiSearchQueryCreator WithSearchQuery(Action<QueryCreatorConfigurator> configurator)
    {
        var query = new QueryCreatorConfigurator(new SearchQuery());
        configurator.Invoke(query);
        baseQuery =   query.Query;
        return this;
    }
    public ISearchQuery<ISearchResult<ISearchModel>> CreateSearchQuery(Action<IBaseQueryConfigurator> configurator)
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
