
using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class FluentQueryConfigurator(ISearchQuery<ISearchResult<ISearchModel>> baseQuery) : IBaseQueryConfigurator
    {
        public ISearchQuery<ISearchResult<ISearchModel>> Query { get; set; } = baseQuery;

        public IDictionary<Occurance, ISearchSubQuery> PostFilterQuery { get; set; } =
            new Dictionary<Occurance, ISearchSubQuery>();
        public Occurance Occurance { get; set; }
    }
}
