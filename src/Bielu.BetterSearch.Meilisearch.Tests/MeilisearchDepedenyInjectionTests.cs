using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.Meilisearch.DepedencyInjection;
using Bielu.BetterSearch.Meilisearch.Services;
using Bielu.BetterSearch.Tests;
using Meilisearch;
using MeilisearchQuery = Meilisearch.SearchQuery;

namespace Bielu.BetterSearch.Meilisearch.Tests;

public class MeilisearchDepedenyInjectionTests(IServiceProvider collection)
    : DepedenyInjectionTestBase<MeilisearchIndexingProviderAsync, MeilisearchClient, MeilisearchQuery>(collection)
{
    protected override void AddProvider(Configurator search) => search.AddMeilisearch();

    protected override void AddProvider(Configurator search, string key) => search.AddMeilisearch(key);

    [Fact]
    public void SearchProviderShouldBeTypeOfMeilisearchSearchProviderAsync() => ServiceShouldBeTypeOf(typeof(ISearchProviderAsync), typeof(MeilisearchSearchProviderAsync));

    [Fact]
    public void IndexingProviderShouldBeTypeOfMeilisearchIndexingProviderAsync() => ServiceShouldBeTypeOf(typeof(IIndexingProviderAsync), typeof(MeilisearchIndexingProviderAsync));
}
