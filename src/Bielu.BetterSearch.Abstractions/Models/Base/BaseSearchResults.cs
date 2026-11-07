namespace Bielu.BetterSearch.Abstractions.Models;

public class BaseSearchResults : ISearchResult
{
    public IEnumerable<ISearchHit> Items { get; set; }
    public IPagination Pagination { get; set; }
}
