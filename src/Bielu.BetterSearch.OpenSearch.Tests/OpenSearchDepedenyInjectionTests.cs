using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.OpenSearch.DepedencyInjection;
using Bielu.BetterSearch.OpenSearch.Services;
using Bielu.BetterSearch.Tests;
using Microsoft.Extensions.DependencyInjection;
using OpenSearch.Client;
using Shouldly;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public class OpenSearchDepedenyInjectionTests(IServiceProvider collection)
    : DepedenyInjectionTestBase<IIndexingProviderAsync, IOpenSearchClient, QueryContainer>(collection)
{
    protected override void AddProvider(Configurator search) => search.AddOpenSearch();

    [Fact]
    public void SearchProviderShouldBeTypeOfOpenSearchSearchProviderAsync() => ServiceShouldBeTypeOf(typeof(ISearchProviderAsync), typeof(OpenSearchSearchProviderAsync));

    [Fact]
    public void IndexingProviderShouldBeTypeOfOpenSearchIndexingProviderAsync() => ServiceShouldBeTypeOf(typeof(IIndexingProviderAsync), typeof(OpenSearchIndexingProviderAsync));

    [Fact]
    public void AddOpenSearch_ClientFactory_IsRegisteredOnce()
    {
        var services = new ServiceCollection().AddBetterSearch(search => search.AddOpenSearch());

        services.Count(descriptor => descriptor.ServiceType == typeof(IClientFactoryAsync<IOpenSearchClient>)).ShouldBe(1);
    }

    [Fact]
    public void AddOpenSearch_ClientFactory_IsSingleton()
    {
        var services = new ServiceCollection().AddBetterSearch(search => search.AddOpenSearch());

        services.Single(descriptor => descriptor.ServiceType == typeof(IClientFactoryAsync<IOpenSearchClient>))
            .Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }
}
