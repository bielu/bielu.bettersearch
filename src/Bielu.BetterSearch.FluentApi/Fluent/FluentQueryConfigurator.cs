
using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class FluentQueryConfigurator : IBaseQueryConfigurator
    {
        public FluentQueryConfigurator(ISearchQuery<ISearchResult<ISearchModel>> baseQuery)
        {
            Query = baseQuery;
        }

        public ISearchQuery<ISearchResult<ISearchModel>> Query { get; set; }

        public IDictionary<Occurance, ISearchSubQuery> PostFilterQuery { get; set; } =
            new Dictionary<Occurance, ISearchSubQuery>();
        public Occurance Occurance { get; set; }
    }
}
