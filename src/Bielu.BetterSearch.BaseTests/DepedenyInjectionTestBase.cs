using Bielu.BetterSearch.Abstractions.Models;
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

    /// <summary>Calls the provider's extension with a key, for example <c>search.AddElasticSearch(key)</c>.</summary>
    protected abstract void AddProvider(Configurator search, string key);

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

        services.Count(descriptor => descriptor.ServiceType == serviceType && !descriptor.IsKeyedService).ShouldBe(1);
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

    [Fact]
    public void AddBetterSearch_ProviderBesideAnother_IsRegisteredUnderItsKey()
    {
        var providerType = ImplementationType<ISearchProviderAsync>(new ServiceCollection().AddBetterSearch(AddProvider));
        var services = new ServiceCollection().AddBetterSearch(search =>
        {
            AddProvider(search);
            search.AddProvider<SecondFakeProvider, SecondFakeProvider>(SecondKey);
        });
        using var provider = services.BuildServiceProvider();
        var configuration = provider.GetRequiredService<SearchApplicationConfiguration>();

        configuration.ProviderKeys.Count.ShouldBe(2);
        configuration.ProviderKeys[1].ShouldBe(SecondKey);
        KeyedImplementationType<ISearchProviderAsync>(services, configuration.ProviderKeys[0]).ShouldBe(providerType);
        KeyedImplementationType<ISearchProviderAsync>(services, SecondKey).ShouldBe(typeof(SecondFakeProvider));
    }

    [Fact]
    public async Task AddBetterSearch_TwoProviders_EachKeyedServiceReachesItsOwnProvider()
    {
        using var provider = TwoFakeProviders().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var token = TestContext.Current.CancellationToken;

        var firstSearch = await scope.ServiceProvider.GetRequiredKeyedService<ISearchServiceAsync>(FirstKey)
            .SearchAsync(new SearchQuery { Index = "any" }, token);
        var secondSearch = await scope.ServiceProvider.GetRequiredKeyedService<ISearchServiceAsync>(SecondKey)
            .SearchAsync(new SearchQuery { Index = "any" }, token);
        var firstExists = await scope.ServiceProvider.GetRequiredKeyedService<IIndexingProviderAsync>(FirstKey)
            .IndexExistsAsync("any", token);
        var secondRemove = await scope.ServiceProvider.GetRequiredKeyedService<IIndexingServiceAsync>(SecondKey)
            .RemoveDocumentAsync(new DeleteDocumentRequest { Id = "1", Type = "any" }, token);

        firstSearch.Errors.ShouldHaveSingleItem().Message.ShouldBe("first provider searched");
        secondSearch.Errors.ShouldHaveSingleItem().Message.ShouldBe("second provider searched");
        firstExists.Errors.ShouldHaveSingleItem().Message.ShouldBe("first provider checked any");
        secondRemove.Errors.ShouldHaveSingleItem().Message.ShouldBe("second provider removed 1");
    }

    [Theory]
    [InlineData(typeof(ISearchServiceAsync))]
    [InlineData(typeof(IIndexingServiceAsync))]
    [InlineData(typeof(ISearchProviderAsync))]
    [InlineData(typeof(IIndexingProviderAsync))]
    public void AddBetterSearch_TwoProvidersAndNoDefault_UnkeyedServiceFailsListingTheKeys(Type serviceType)
    {
        using var provider = TwoFakeProviders().BuildServiceProvider();
        using var scope = provider.CreateScope();

        var exception = Should.Throw<InvalidOperationException>(() => scope.ServiceProvider.GetService(serviceType));

        exception.Message.ShouldContain("More than one search provider is registered ('first', 'second') and none is the default");
        exception.Message.ShouldContain("SetDefaultProvider(\"first\")");
    }

    [Fact]
    public async Task AddBetterSearch_TwoProvidersWithDefault_UnkeyedServicesUseTheDefault()
    {
        using var provider = TwoFakeProviders(search => search.SetDefaultProvider(SecondKey)).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var token = TestContext.Current.CancellationToken;

        var search = await scope.ServiceProvider.GetRequiredService<ISearchServiceAsync>()
            .SearchAsync(new SearchQuery { Index = "any" }, token);
        var exists = await scope.ServiceProvider.GetRequiredService<IIndexingProviderAsync>().IndexExistsAsync("any", token);

        search.Errors.ShouldHaveSingleItem().Message.ShouldBe("second provider searched");
        exists.Errors.ShouldHaveSingleItem().Message.ShouldBe("second provider checked any");
        provider.GetRequiredService<SearchApplicationConfiguration>().SearchProviderType.ShouldBe(typeof(SecondFakeProvider));
    }

    [Fact]
    public async Task AddBetterSearch_OneKeyedProvider_UnkeyedAndKeyedServicesReachIt()
    {
        using var provider = new ServiceCollection()
            .AddBetterSearch(search => search.AddProvider<FirstFakeProvider, FirstFakeProvider>(FirstKey))
            .BuildServiceProvider();
        using var scope = provider.CreateScope();
        var token = TestContext.Current.CancellationToken;

        var unkeyed = await scope.ServiceProvider.GetRequiredService<ISearchServiceAsync>()
            .SearchAsync(new SearchQuery { Index = "any" }, token);
        var keyed = await scope.ServiceProvider.GetRequiredKeyedService<ISearchServiceAsync>(FirstKey)
            .SearchAsync(new SearchQuery { Index = "any" }, token);

        unkeyed.Errors.ShouldHaveSingleItem().Message.ShouldBe("first provider searched");
        keyed.Errors.ShouldHaveSingleItem().Message.ShouldBe("first provider searched");
    }

    [Fact]
    public async Task AddBetterSearch_TwoProviders_ObserversArePerKey()
    {
        var token = TestContext.Current.CancellationToken;
        var observer = new RecordingObserver<SearchDocument>();
        using var provider = TwoFakeProviders().BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            var first = scope.ServiceProvider.GetRequiredKeyedService<IIndexingServiceAsync>(FirstKey);
            var second = scope.ServiceProvider.GetRequiredKeyedService<IIndexingServiceAsync>(SecondKey);
            first.ShouldNotBeSameAs(second);
            using var subscription = first.Subscribe(observer);

            (await second.IndexDocumentAsync(new SearchDocument { Id = "second", Type = "TestDocument", Index = "any" }, token)).IsSuccess.ShouldBeTrue();
            (await first.IndexDocumentAsync(new SearchDocument { Id = "first", Type = "TestDocument", Index = "any" }, token)).IsSuccess.ShouldBeTrue();
        }

        observer.Values.ShouldHaveSingleItem().Id.ShouldBe("first");
    }

    [Fact]
    public async Task AddBetterSearch_ValidatorRegisteredForOneKey_ValidatesOnlyThatKey()
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped<IDocumentValidatorAsync, RejectingDocumentValidator>(FirstKey);
        TwoFakeProviders(services: services);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var token = TestContext.Current.CancellationToken;
        var document = new SearchDocument { Id = "1", Type = "TestDocument", Index = "any" };

        var first = await scope.ServiceProvider.GetRequiredKeyedService<IIndexingServiceAsync>(FirstKey).IndexDocumentAsync(document, token);
        var second = await scope.ServiceProvider.GetRequiredKeyedService<IIndexingServiceAsync>(SecondKey).IndexDocumentAsync(document, token);

        first.Errors.ShouldHaveSingleItem().Message.ShouldBe("Document 1 is not valid.");
        second.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task AddBetterSearch_UnkeyedValidatorAndServiceTypes_ApplyToEveryKey()
    {
        var services = new ServiceCollection();
        services.AddScoped<IDocumentValidatorAsync, RejectingDocumentValidator>();
        TwoFakeProviders(search => search.SetIndexingServiceType<CustomIndexingService>().SetSearchServiceType<CustomSearchService>(), services);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var token = TestContext.Current.CancellationToken;

        var indexing = scope.ServiceProvider.GetRequiredKeyedService<IIndexingServiceAsync>(SecondKey);
        var index = await indexing.IndexDocumentAsync(new SearchDocument { Id = "1", Type = "TestDocument", Index = "any" }, token);

        indexing.ShouldBeOfType<CustomIndexingService>();
        scope.ServiceProvider.GetRequiredKeyedService<ISearchServiceAsync>(SecondKey).ShouldBeOfType<CustomSearchService>();
        index.Errors.ShouldHaveSingleItem().Message.ShouldBe("Document 1 is not valid.");
    }

    [Fact]
    public void AddProvider_KeyAlreadyUsed_Throws()
    {
        var exception = Should.Throw<ArgumentException>(() => new ServiceCollection().AddBetterSearch(search =>
            search.AddProvider<FirstFakeProvider, FirstFakeProvider>(FirstKey)
                .AddProvider<SecondFakeProvider, SecondFakeProvider>("FIRST")));

        exception.Message.ShouldContain("already registered under the key 'FIRST'");
    }

    [Fact]
    public void AddProvider_SameProviderTwice_Throws()
    {
        var exception = Should.Throw<InvalidOperationException>(() => new ServiceCollection().AddBetterSearch(search =>
            search.AddProvider<FirstFakeProvider, FirstFakeProvider>(FirstKey)
                .AddProvider<FirstFakeProvider, FirstFakeProvider>("other")));

        exception.Message.ShouldContain("FirstFakeProvider is already registered under the key 'first'");
    }

    [Fact]
    public void SetDefaultProvider_UnknownKey_AddBetterSearchThrows()
    {
        var exception = Should.Throw<InvalidOperationException>(() => TwoFakeProviders(search => search.SetDefaultProvider("third")));

        exception.Message.ShouldBe("SetDefaultProvider names 'third', but no provider is registered under that key. Registered keys: 'first', 'second'.");
    }

    [Fact]
    public void AddBetterSearch_ProviderWithKey_IsRegisteredUnderThatKey()
    {
        var providerType = ImplementationType<ISearchProviderAsync>(new ServiceCollection().AddBetterSearch(AddProvider));
        var services = new ServiceCollection().AddBetterSearch(search => AddProvider(search, "products"));
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<SearchApplicationConfiguration>().ProviderKeys.ShouldHaveSingleItem().ShouldBe("products");
        KeyedImplementationType<ISearchProviderAsync>(services, "products").ShouldBe(providerType);
        ImplementationType<ISearchProviderAsync>(services).ShouldBe(providerType);
    }

    private const string FirstKey = "first";
    private const string SecondKey = "second";

    private static IServiceCollection TwoFakeProviders(Action<Configurator>? configure = null, IServiceCollection? services = null) =>
        (services ?? new ServiceCollection()).AddBetterSearch(search =>
        {
            search.AddProvider<FirstFakeProvider, FirstFakeProvider>(FirstKey)
                .AddProvider<SecondFakeProvider, SecondFakeProvider>(SecondKey);
            configure?.Invoke(search);
        });

    private static Type? ImplementationType<TService>(IServiceCollection services) =>
        services.Single(descriptor => descriptor.ServiceType == typeof(TService) && !descriptor.IsKeyedService).ImplementationType;

    private static Type? KeyedImplementationType<TService>(IServiceCollection services, string key) =>
        services.Single(descriptor => descriptor.ServiceType == typeof(TService) && descriptor.IsKeyedService && Equals(descriptor.ServiceKey, key))
            .KeyedImplementationType;

    private sealed class FirstFakeProvider() : FakeProvider("first");

    private sealed class SecondFakeProvider() : FakeProvider("second");

    /// <summary>Indexes every document; every other call fails with a message naming the provider and the call.</summary>
    private abstract class FakeProvider(string name) : ISearchProviderAsync, IIndexingProviderAsync
    {
        public Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Fail<ISearchResult>($"{name} provider searched"));

        public Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Ok());

        public Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> documents, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Fail<int>($"{name} provider indexed many"));

        public Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Fail($"{name} provider removed {id}"));

        public Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Fail<int>($"{name} provider removed many"));

        public Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest index, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Fail<long?>($"{name} provider removed all"));

        public Task<Result<bool>> EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Fail<bool>($"{name} provider ensured {index}"));

        public Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Fail<bool>($"{name} provider deleted {index}"));

        public Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Fail<bool>($"{name} provider checked {index}"));

        public Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Fail<bool>($"{name} provider created {index}"));
    }

    private sealed class RejectingDocumentValidator : IDocumentValidatorAsync
    {
        public Task<Result<bool>> ValidateDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) =>
            Task.FromResult<Result<bool>>(false);
    }

    private sealed class CustomIndexingService(IIndexingProviderAsync indexingProvider, IDocumentValidatorAsync documentValidator)
        : IndexingServiceAsync(indexingProvider, documentValidator);

    private sealed class CustomSearchService(ISearchProviderAsync providerAsync) : SearchServiceAsync(providerAsync);

    private sealed class CustomDocumentValidator : IDocumentValidatorAsync
    {
        public Task<Result<bool>> ValidateDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) =>
            Task.FromResult<Result<bool>>(true);
    }
}
