using Bielu.BetterSearch.Abstractions.Results;

namespace Bielu.BetterSearch.FluentApi;

public interface IFluentApiSearchQueryCreator
{
    ISearchQuery<IQueryResult> CreateSearchQuery(Action<IBaseQueryConfigurator> configurator);
}
