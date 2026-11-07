using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Meilisearch.Services;
using Bielu.BetterSearch.Tests;
using Meilisearch;
using MeilisearchQuery = Meilisearch.SearchQuery;

namespace Bielu.BetterSearch.Meilisearch.Tests;

public class MeilisearchDepedenyInjectionTests(IServiceProvider collection)
    : DepedenyInjectionTestBase<MeilisearchIndexingProviderAsync, MeilisearchClient, MeilisearchQuery>(collection)
{
    [Fact]
    public void SearchProviderShouldBeTypeOfMeilisearchSearchProviderAsync() => ServiceShouldBeTypeOf(typeof(ISearchProviderAsync), typeof(MeilisearchSearchProviderAsync));

    [Fact]
    public void IndexingProviderShouldBeTypeOfMeilisearchIndexingProviderAsync() => ServiceShouldBeTypeOf(typeof(IIndexingProviderAsync), typeof(MeilisearchIndexingProviderAsync));
}
