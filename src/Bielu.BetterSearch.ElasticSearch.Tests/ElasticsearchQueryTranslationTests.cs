using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport.Extensions;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public sealed class ElasticsearchQueryTranslationTests(IEnumerable<ISubQueryTranslator<Query>> translators)
    : SubQueryTranslationTestBase<Query>(translators)
{
    protected override string Render(Query translated)
        => new ElasticsearchClient().RequestResponseSerializer.SerializeToString(translated);
}
