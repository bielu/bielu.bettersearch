
using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.FluentApi;

/// <summary>Builds an <see cref="ISearchQuery"/> through the fluent API.</summary>
/// <param name="baseQuery">The search query to build on.</param>
public class FluentApiSearchQueryCreator(ISearchQuery baseQuery) : IFluentApiSearchQueryCreator
{
    /// <summary>Configures the paging, index, sorting and returned properties of the search query.</summary>
    /// <param name="configurator">Configures the search query.</param>
    /// <returns>This creator, for chaining.</returns>
    public FluentApiSearchQueryCreator WithSearchQuery(Action<QueryCreatorConfigurator> configurator)
    {
        var query = new QueryCreatorConfigurator(baseQuery);
        configurator.Invoke(query);
        baseQuery = query.Query;
        return this;
    }
    /// <inheritdoc />
    public ISearchQuery CreateSearchQuery(Action<IBaseQueryConfigurator> configurator)
    {
        var newQuery = new FluentQueryConfigurator(baseQuery);
        newQuery.Occurrence = Occurrence.Must;
        configurator.Invoke(newQuery);
        return newQuery.Query;
    }
    /// <summary>Creates a creator over a new, empty <see cref="SearchQuery"/>.</summary>
    /// <returns>A new <see cref="FluentApiSearchQueryCreator"/>.</returns>
    public static FluentApiSearchQueryCreator Create()
    {
        return new FluentApiSearchQueryCreator(new SearchQuery { Index = string.Empty });
    }
}
