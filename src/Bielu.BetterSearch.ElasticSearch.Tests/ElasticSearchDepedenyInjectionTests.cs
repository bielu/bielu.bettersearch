using Bielu.BetterSearch.ElasticSearch.Services;
using Bielu.BetterSearch.Tests;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public class ElasticSearchDepedenyInjectionTests(IServiceProvider collection)
    : DepedenyInjectionTestBase<ElasticSearchIndexingProviderAsync, ElasticsearchClient, BoolQueryDescriptor<SearchDocument>>(collection);
