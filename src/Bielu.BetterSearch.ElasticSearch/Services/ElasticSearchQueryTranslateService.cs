using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FluentResults;

namespace Bielu.BetterSearch.ElasticSearch.Services;

public class ElasticSearchQueryTranslateService: IQueryTranslateServiceAsync<BoolQueryDescriptor<SearchDocument>>
{
    public async Task<Result<BoolQueryDescriptor<SearchDocument>>> TranslateMainQuery(ISearchQuery<ISearchResult<ISearchModel>> query) {
        var elasticQuery = new BoolQueryDescriptor<SearchDocument>().MustNot();
        return query switch
        {
            SearchQuery searchQuery => Result.Ok(elasticQuery),
            _ => Result.Fail("Query type not supported")
        };
    }
}
