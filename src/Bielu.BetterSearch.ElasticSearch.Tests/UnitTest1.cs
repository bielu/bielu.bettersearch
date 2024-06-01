using Bielu.BetterSearch.Lifti.Services;
using Bielu.BetterSearch.Tests;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Testcontainers.Elasticsearch;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public class UnitTest1(IServiceProvider collection) : DepedenyInjectionTestBase<ElasticSearchIndexingProviderAsync,ElasticsearchClient,Query>(collection)
{
    private readonly ElasticsearchContainer _elasticsearch
        = new ElasticsearchBuilder().Build();
    [Fact]
    public void Test1()
    {
    }

}
