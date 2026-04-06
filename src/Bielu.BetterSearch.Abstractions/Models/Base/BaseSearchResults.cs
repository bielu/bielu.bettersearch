namespace Bielu.BetterSearch.Abstractions.Models;

public class BaseSearchResults : ISearchResult<BaseSearchModel>, ISearchResult<ISearchModel>
{
    public IEnumerable<ISearchModel> Items { get; set; }
    public IPagination Pagination { get; set; }
}
