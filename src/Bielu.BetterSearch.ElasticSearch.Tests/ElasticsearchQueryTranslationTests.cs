using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public sealed class ElasticsearchQueryTranslationTests(IEnumerable<ISubQueryTranslator<Query>> translators)
    : SubQueryTranslationTestBase<Query>(translators);
