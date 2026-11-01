using Bielu.BetterSearch.Abstractions.Services;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.BetterSearch.Tests;

public abstract class DepedenyInjectionTestBase<TSearchServiceType,TSearchClient, TQueryType>(IServiceProvider collection)
{
    public void ServiceShouldBeRegistered(Type serviceType)
    {
        var instance = collection.GetService(serviceType);
        instance.ShouldNotBeNull($"because {serviceType.Name} should be registered in the service provider");
    }
    public void ServiceShouldBeTypeOf(Type interfaceType, Type implementationType)
    {
        var instance = collection.GetService(interfaceType);
        instance.ShouldBeOfType(implementationType, $"because {interfaceType.Name} should be registered as {implementationType.Name}");
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

    [Fact]
    public void SearchServiceShouldBeRegistered() => ServiceShouldBeRegistered(typeof(ISearchServiceAsync));

    [Fact]
    public void SearchProviderShouldBeRegistered() => ServiceShouldBeRegistered(typeof(ISearchProviderAsync));



}
