using SImpl.SearchModule.Abstraction.Results;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class QueryCreatorConfigurator
    {
        public QueryCreatorConfigurator(ISearchQuery<IQueryResult> query)
        {
            Query = query;
        }

        public ISearchQuery<IQueryResult> Query { get; set; }

        public QueryCreatorConfigurator WithPageSize(int pageSize)
        {
            Query.PageSize = pageSize;
            return this;
        }
        public QueryCreatorConfigurator WithPage(int page)
        {
            Query.Page = page;
            return this;
        }
        public QueryCreatorConfigurator WithIndex(string indexName)
        {
            Query.Index = indexName;
            return this;
        }
    }
}
