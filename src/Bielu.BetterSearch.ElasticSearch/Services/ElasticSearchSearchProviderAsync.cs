using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FluentResults;

namespace Bielu.BetterSearch.ElasticSearch.Services;

public class ElasticSearchSearchProviderAsync(IQueryTranslateServiceAsync<BoolQueryDescriptor<SearchDocument>> queryTranslateServiceAsync, IClientFactoryAsync<ElasticsearchClient> clientFactoryAsync) : ISearchProviderAsync
{
    public Task<Result<ISearchResult<ISearchModel>>> SearchAsync(ISearchQuery<ISearchResult<ISearchModel>> query) => throw new NotImplementedException();
}
