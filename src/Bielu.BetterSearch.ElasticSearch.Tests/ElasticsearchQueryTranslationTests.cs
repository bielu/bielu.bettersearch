using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport;
using Elastic.Transport.Extensions;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public sealed class ElasticsearchQueryTranslationTests(IEnumerable<ISubQueryTranslator<Query>> translators)
    : SubQueryTranslationTestBase<Query>(translators)
{
    private static readonly Serializer Serializer = new ElasticsearchClient().RequestResponseSerializer;

    protected override string Render(Query translated) => Serializer.SerializeToString(translated);
}
