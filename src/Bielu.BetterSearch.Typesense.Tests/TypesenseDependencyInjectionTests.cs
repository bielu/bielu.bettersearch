using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.Tests;
using Bielu.BetterSearch.Typesense.DependencyInjection;
using Bielu.BetterSearch.Typesense.Services;

namespace Bielu.BetterSearch.Typesense.Tests;

public class TypesenseDependencyInjectionTests(IServiceProvider collection)
    : DependencyInjectionTestBase(collection, typeof(TypesenseHttpClientWrapper), typeof(TypesenseSearchRequest))
{
    protected override void AddProvider(Configurator search) => search.AddTypesense();

    protected override void AddProvider(Configurator search, string key) => search.AddTypesense(key);

    [Fact]
    public void SearchProviderShouldBeTypeOfTypesenseSearchProviderAsync() => ServiceShouldBeTypeOf(typeof(ISearchProviderAsync), typeof(TypesenseSearchProviderAsync));

    [Fact]
    public void IndexingProviderShouldBeTypeOfTypesenseIndexingProviderAsync() => ServiceShouldBeTypeOf(typeof(IIndexingProviderAsync), typeof(TypesenseIndexingProviderAsync));
}
