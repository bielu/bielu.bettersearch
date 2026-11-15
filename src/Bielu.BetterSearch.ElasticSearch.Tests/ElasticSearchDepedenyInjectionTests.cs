using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.DepedencyInjection;
using Bielu.BetterSearch.ElasticSearch.Services;
using Bielu.BetterSearch.Tests;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public class ElasticSearchDepedenyInjectionTests(IServiceProvider collection)
    : DepedenyInjectionTestBase(collection, typeof(ElasticsearchClient), typeof(Query))
{
    protected override void AddProvider(Configurator search) => search.AddElasticSearch();

    protected override void AddProvider(Configurator search, string key) => search.AddElasticSearch(key);
}
