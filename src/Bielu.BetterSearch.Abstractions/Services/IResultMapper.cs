using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.Abstractions.Services;

public interface IResultMapper<T>
{
    ISearchResult<ISearchModel> MapResult(T searchEngineResult);
}
