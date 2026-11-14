using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.Tests;
using Bielu.BetterSearch.Typesense.DepedencyInjection;
using Bielu.BetterSearch.Typesense.Services;

namespace Bielu.BetterSearch.Typesense.Tests;

public class TypesenseDepedenyInjectionTests(IServiceProvider collection)
    : DepedenyInjectionTestBase<TypesenseIndexingProviderAsync, TypesenseHttpClientWrapper, TypesenseSearchRequest>(collection)
{
    protected override void AddProvider(Configurator search) => search.AddTypesense();

    protected override void AddProvider(Configurator search, string key) => search.AddTypesense(key);

    [Fact]
    public void SearchProviderShouldBeTypeOfTypesenseSearchProviderAsync() => ServiceShouldBeTypeOf(typeof(ISearchProviderAsync), typeof(TypesenseSearchProviderAsync));

    [Fact]
    public void IndexingProviderShouldBeTypeOfTypesenseIndexingProviderAsync() => ServiceShouldBeTypeOf(typeof(IIndexingProviderAsync), typeof(TypesenseIndexingProviderAsync));
}
