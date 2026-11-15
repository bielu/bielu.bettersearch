
using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.FluentApi;

public class FluentApiSearchQueryCreator(ISearchQuery baseQuery) : IFluentApiSearchQueryCreator
{
    public FluentApiSearchQueryCreator WithSearchQuery(Action<QueryCreatorConfigurator> configurator)
    {
        var query = new QueryCreatorConfigurator(baseQuery);
        configurator.Invoke(query);
        baseQuery = query.Query;
        return this;
    }
    public ISearchQuery CreateSearchQuery(Action<IBaseQueryConfigurator> configurator)
    {
        var newQuery = new FluentQueryConfigurator(baseQuery);
        newQuery.Occurrence = Occurrence.Must;
        configurator.Invoke(newQuery);
        return newQuery.Query;
    }
    public static FluentApiSearchQueryCreator Create()
    {
        return new FluentApiSearchQueryCreator(new SearchQuery());
    }
}
