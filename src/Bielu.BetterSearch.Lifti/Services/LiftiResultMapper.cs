using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Lifti;

namespace Bielu.BetterSearch.Lifti.Services;

/// <summary>
/// Maps Lifti's matches to hits holding a <see cref="SearchDocument"/> with the document id and the query's index:
/// Lifti keeps no type or fields, so the type is <c>null</c> and the fields are empty.
/// </summary>
internal sealed class LiftiResultMapper : IResultMapper<ISearchResults<string>>
{
    public ISearchResult MapResult(ISearchResults<string> searchEngineResult, ISearchQuery query) => new BaseSearchResults
    {
        Items = searchEngineResult.Select(x => (ISearchHit)new SearchHit(new SearchDocument { Id = x.Key, Index = query.Index }, x.Score)).ToList(),
        Pagination = new Pagination
        {
            TotalCount = searchEngineResult.Count
        }
    };
}
