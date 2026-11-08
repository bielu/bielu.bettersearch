using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using OpenSearch.Client;
using OpenSearch.Net;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public sealed class OpenSearchQueryTranslationTests(IEnumerable<ISubQueryTranslator<QueryContainer>> translators)
    : SubQueryTranslationTestBase<QueryContainer>(translators)
{
    protected override string Render(QueryContainer translated)
        => new OpenSearchClient().RequestResponseSerializer.SerializeToString(new SearchRequest("people") { Query = translated });
}
