using SImpl.SearchModule.Abstraction.Results;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class FluentQueryConfigurator : IBaseQueryConfigurator
    {
        public FluentQueryConfigurator(ISearchQuery<IQueryResult> baseQuery)
        {
            Query = baseQuery;
        }

        public ISearchQuery<IQueryResult> Query { get; set; }

        public IDictionary<Occurance, ISearchSubQuery> PostFilterQuery { get; set; } =
            new Dictionary<Occurance, ISearchSubQuery>();
        public Occurance Occurance { get; set; }
    }
}
