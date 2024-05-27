using Bielu.BetterSearch.Abstractions.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.BetterSearch.Tests;

public abstract class DepedenyInjectionTestBase<TSearchServiceType,TSearchClient, TQueryType>(IServiceProvider collection)
{
    private void ServiceShouldBeRegistered(Type serviceType)
    {
        var instance = collection.GetService(serviceType);
        instance.Should().NotBeNull($"because {serviceType.Name} should be registered in the service provider");
    }

    [Fact]
    public void IndexingProviderShouldBeRegistered() => ServiceShouldBeRegistered(typeof(IIndexingProviderAsync));

    [Fact]
    public void ClientFactoryShouldBeRegistered() => ServiceShouldBeRegistered(typeof(IClientFactoryAsync<TSearchClient>));

    [Fact]
    public void DocumentValidatorShouldBeRegistered() => ServiceShouldBeRegistered(typeof(IDocumentValidatorAsync));

    [Fact]
    public void IndexingServiceShouldBeRegistered() => ServiceShouldBeRegistered(typeof(IIndexingServiceAsync));

    [Fact]
    public void QueryTranslateServiceShouldBeRegistered() => ServiceShouldBeRegistered(typeof(IQueryTranslateServiceAsync<TQueryType>));
}
