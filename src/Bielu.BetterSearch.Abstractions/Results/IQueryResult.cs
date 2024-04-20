using System.Collections.Generic;
using Bielu.BetterSearch.Abstractions.Models;

namespace SImpl.SearchModule.Abstraction.Results
{
    public interface IQueryResult
    {
        List<ISearchModel> SearchModels { get; set; }
        Pagination Pagination { get; set; }
    }
}
