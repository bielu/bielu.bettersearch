using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.Abstractions.Results
{
    public interface IQueryResult
    {
        List<ISearchModel> SearchModels { get; set; }
        Pagination Pagination { get; set; }
    }
}
