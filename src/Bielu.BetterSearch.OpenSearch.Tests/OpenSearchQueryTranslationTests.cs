using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using OpenSearch.Client;
using OpenSearch.Net;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public sealed class OpenSearchQueryTranslationTests(IEnumerable<ISubQueryTranslator<QueryContainer>> translators)
    : SubQueryTranslationTestBase<QueryContainer>(translators)
{
    private static readonly IOpenSearchSerializer Serializer = new OpenSearchClient().RequestResponseSerializer;

    protected override string Render(QueryContainer translated)
        => Serializer.SerializeToString(new SearchRequest("people") { Query = translated });
}
