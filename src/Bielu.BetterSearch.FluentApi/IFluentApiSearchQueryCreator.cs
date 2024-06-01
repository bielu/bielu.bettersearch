
using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.FluentApi;

public interface IFluentApiSearchQueryCreator
{
    ISearchQuery<ISearchResult<ISearchModel>> CreateSearchQuery(Action<IBaseQueryConfigurator> configurator);
}
