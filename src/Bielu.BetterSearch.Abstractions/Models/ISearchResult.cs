namespace Bielu.BetterSearch.Abstractions.Models;

public interface ISearchResult
{
    IEnumerable<ISearchHit> Items { get; set; }
    IPagination Pagination { get; set; }
}
