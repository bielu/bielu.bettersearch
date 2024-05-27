using Bielu.BetterSearch.Abstractions.Services;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace Bielu.BetterSearch.ElasticSearch.Services;

public class ElasticSearchQueryTranslateService: IQueryTranslateServiceAsync<Query>
{
    public Task<Query> TranslateQuery(string query) => throw new NotImplementedException();
}
