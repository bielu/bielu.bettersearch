using Bielu.BetterSearch.Abstractions.Fields;

namespace Bielu.BetterSearch.EndpointApi.Models;

public class SearchRequest
{
    public string? Term { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public IEnumerable<IFilter>? Filters { get; set; }
    public IEnumerable<SortOrderField>? Sorts { get; set; }
}
