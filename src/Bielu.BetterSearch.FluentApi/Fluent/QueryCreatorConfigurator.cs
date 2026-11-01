using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class QueryCreatorConfigurator(ISearchQuery<ISearchResult<ISearchModel>> query)
    {
        public ISearchQuery<ISearchResult<ISearchModel>> Query { get; set; } = query;

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
