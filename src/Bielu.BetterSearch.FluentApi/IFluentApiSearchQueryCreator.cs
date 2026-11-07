
using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.FluentApi;

public interface IFluentApiSearchQueryCreator
{
    ISearchQuery CreateSearchQuery(Action<IBaseQueryConfigurator> configurator);
}
