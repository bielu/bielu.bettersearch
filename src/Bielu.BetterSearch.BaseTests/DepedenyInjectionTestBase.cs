using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Abstractions.Services.NullImplementations;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.Services;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

public abstract class DepedenyInjectionTestBase<TSearchServiceType, TSearchClient, TQueryType>(IServiceProvider collection)
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

    /// <summary>Calls the provider's extension, for example <c>search.AddElasticSearch()</c>.</summary>
    protected abstract void AddProvider(Configurator search);

    [Theory]
    [InlineData(typeof(IDocumentValidatorAsync))]
    [InlineData(typeof(IIndexingServiceAsync))]
    [InlineData(typeof(ISearchServiceAsync))]
    [InlineData(typeof(ISearchProviderAsync))]
    [InlineData(typeof(IIndexingProviderAsync))]
    [InlineData(typeof(SearchApplicationConfiguration))]
    public void AddBetterSearch_SharedServices_AreRegisteredOnce(Type serviceType)
    {
        var services = new ServiceCollection().AddBetterSearch(AddProvider);

        services.Count(descriptor => descriptor.ServiceType == serviceType).ShouldBe(1);
    }

    [Fact]
    public void AddBetterSearch_TwoContainersInOneProcess_EachResolvesItsOwnProvider()
    {
        var withProvider = new ServiceCollection().AddBetterSearch(AddProvider);
        var withoutProvider = new ServiceCollection().AddBetterSearch(_ => { });

        var providerType = ImplementationType<ISearchProviderAsync>(withProvider);
        providerType.ShouldNotBe(typeof(DummySearchProviderAsync));
        ImplementationType<ISearchProviderAsync>(withoutProvider).ShouldBe(typeof(DummySearchProviderAsync));
        ImplementationType<IIndexingProviderAsync>(withoutProvider).ShouldBe(typeof(DummyIndexingProviderAsync));
        // Registering the second container must not change the first one's configuration.
        using var first = withProvider.BuildServiceProvider();
        using var second = withoutProvider.BuildServiceProvider();
        var firstConfiguration = first.GetRequiredService<SearchApplicationConfiguration>();
        firstConfiguration.SearchProviderType.ShouldBe(providerType);
        firstConfiguration.ShouldNotBeSameAs(second.GetRequiredService<SearchApplicationConfiguration>());
    }

    [Fact]
    public void AddBetterSearch_ServiceTypesSetAfterProvider_AreTheOnlyRegistrations()
    {
        var services = new ServiceCollection().AddBetterSearch(search =>
        {
            AddProvider(search);
            search.SetIndexingServiceType<CustomIndexingService>()
                .SetSearchServiceType<CustomSearchService>()
                .SetDocumentValidatorType<CustomDocumentValidator>();
        });

        ImplementationType<IIndexingServiceAsync>(services).ShouldBe(typeof(CustomIndexingService));
        ImplementationType<ISearchServiceAsync>(services).ShouldBe(typeof(CustomSearchService));
        ImplementationType<IDocumentValidatorAsync>(services).ShouldBe(typeof(CustomDocumentValidator));
    }

    [Fact]
    public void AddBetterSearch_ValidatorRegisteredBefore_IsKept()
    {
        var services = new ServiceCollection();
        services.AddScoped<IDocumentValidatorAsync, CustomDocumentValidator>();

        services.AddBetterSearch(AddProvider);

        ImplementationType<IDocumentValidatorAsync>(services).ShouldBe(typeof(CustomDocumentValidator));
    }

    [Fact]
    public async Task AddBetterSearch_NoProvider_SearchAndIndexReturnFailedResult()
    {
        using var provider = new ServiceCollection().AddBetterSearch(_ => { }).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var token = TestContext.Current.CancellationToken;

        var search = await scope.ServiceProvider.GetRequiredService<ISearchServiceAsync>()
            .SearchAsync(new SearchQuery { Index = "any" }, token);
        var indexing = scope.ServiceProvider.GetRequiredService<IIndexingServiceAsync>();
        var index = await indexing.IndexDocumentAsync(
            new SearchDocument { Id = "1", Type = "TestDocument", Index = "any" }, token);
        var remove = await indexing.RemoveDocumentAsync(new DeleteDocumentRequest { Id = "1", Type = "any" }, token);

        search.IsFailed.ShouldBeTrue();
        search.Errors.ShouldContain(error => error.Message.Contains("No search provider is registered"));
        index.IsFailed.ShouldBeTrue();
        index.Errors.ShouldContain(error => error.Message.Contains("No indexing provider is registered"));
        remove.IsFailed.ShouldBeTrue();
        remove.Errors.ShouldContain(error => error.Message.Contains("No indexing provider is registered"));
    }

    private static Type? ImplementationType<TService>(IServiceCollection services) =>
        services.Single(descriptor => descriptor.ServiceType == typeof(TService)).ImplementationType;

    private sealed class CustomIndexingService(IIndexingProviderAsync indexingProvider, IDocumentValidatorAsync documentValidator)
        : IndexingServiceAsync(indexingProvider, documentValidator);

    private sealed class CustomSearchService(ISearchProviderAsync providerAsync) : SearchServiceAsync(providerAsync);

    private sealed class CustomDocumentValidator : IDocumentValidatorAsync
    {
        public Task<Result<bool>> ValidateDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) =>
            Task.FromResult<Result<bool>>(true);
    }
}
