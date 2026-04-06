using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Services;
using Lifti;

namespace Bielu.BetterSearch.Lifti.Services;

public class LiftiBaseSearchModelMapper : IResultMapper<ISearchResults<string>>
{
    public ISearchResult<ISearchModel> MapResult(ISearchResults<string> searchEngineResult) => new BaseSearchResults()
    {
        Items = searchEngineResult.Select(x => new BaseSearchModel() { Id = x.Key }),
        Pagination = new Pagination
        {
            TotalCount = searchEngineResult.Count
        }
    };
}
