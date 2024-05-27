namespace Bielu.BetterSearch.Abstractions.Models;

public interface ISearchResult<T> where T : ISearchModel
{
    IEnumerable<ISearchModel> Items { get; set; }
    IPagination Pagination { get; set; }
}
