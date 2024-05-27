using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.Abstractions.Results
{
    public class SimplQueryResult : IQueryResult
    {
        public List<ISearchModel> SearchModels { get; set; }
        public List<IAggregation> Aggregations { get; set; } = [];
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public string Culture { get; set; }
        public string Term { get; set; }
        public Pagination Pagination { get; set; }
        public List<Sort> Sorts { get; set; }
        public HighLighter HighLighter { get; set; } = new();
    }
}
