using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.OpenSearch.Services;
using Bielu.BetterSearch.Tests;
using OpenSearch.Client;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public class OpenSearchDepedenyInjectionTests(IServiceProvider collection)
    : DepedenyInjectionTestBase<OpenSearchIndexingProviderAsync, IOpenSearchClient, BoolQueryDescriptor<SearchDocument>>(collection)
{
    [Fact]
    public void SearchProviderShouldBeTypeOfOpenSearchSearchProviderAsync() => ServiceShouldBeTypeOf(typeof(ISearchProviderAsync), typeof(OpenSearchSearchProviderAsync));

    [Fact]
    public void IndexingProviderShouldBeTypeOfOpenSearchIndexingProviderAsync() => ServiceShouldBeTypeOf(typeof(IIndexingProviderAsync), typeof(OpenSearchIndexingProviderAsync));
}
