using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.DepedencyInjection;
using Bielu.BetterSearch.ElasticSearch.Services;
using Bielu.BetterSearch.Tests;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public class ElasticSearchDepedenyInjectionTests(IServiceProvider collection)
    : DepedenyInjectionTestBase<ElasticSearchIndexingProviderAsync, ElasticsearchClient, Query>(collection)
{
    protected override void AddProvider(Configurator search) => search.AddElasticSearch();
}
