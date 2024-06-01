using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FluentResults;

namespace Bielu.BetterSearch.ElasticSearch.Services;

public class ElasticSearchSearchProviderAsync(IQueryTranslateServiceAsync<BoolQueryDescriptor<SearchDocument>> queryTranslateServiceAsync, IClientFactoryAsync<ElasticsearchClient> clientFactoryAsync) : ISearchProviderAsync
{
    public async Task<Result<ISearchResult<ISearchModel>>> SearchAsync(ISearchQuery<ISearchModel> query)
    {
        var client = clientFactoryAsync.GetOrCreateClientAsync(query.Index);
        var elasticQuery = await client.Result.SearchAsync<SearchDocument>(
            x=> x.Query(
                (await queryTranslateServiceAsync.TranslateMainQuery(query)).Value));
    }
}
