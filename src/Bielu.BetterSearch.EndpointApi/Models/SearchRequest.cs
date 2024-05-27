using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.EndpointApi.Models;

public class SearchRequest
{
    public string? Term { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public IEnumerable<IFilter>?  Filters { get; set; }
    public IEnumerable<Sort>? Sorts { get; set; }
}
