using Bielu.BetterSearch.Abstractions.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.BetterSearch.Configuration;

public class Configurator(IServiceCollection services, IConfiguration configuration)
{
    public IServiceCollection Services => services;
    public IConfiguration AppConfiguration => configuration;

    // Each AddBetterSearch call gets its own configuration, so two containers in one process don't share provider types.
    public SearchApplicationConfiguration Configuration { get; } = new();

    /// <summary>
    /// Registers a provider under <paramref name="key"/>: <c>AddBetterSearch</c> then registers its search and
    /// indexing provider, and a search service, an indexing service and a document validator built on them, as keyed
    /// services under that key. The unkeyed services use this provider when it is the only one, or when
    /// <see cref="SetDefaultProvider"/> names it.
    /// </summary>
    /// <exception cref="ArgumentException">The key is empty, or already used (keys are compared ignoring case).</exception>
    /// <exception cref="InvalidOperationException">The same provider is already registered under another key.</exception>
    public Configurator AddProvider<TSearchProvider, TIndexingProvider>(string key)
        where TSearchProvider : class, ISearchProviderAsync
        where TIndexingProvider : class, IIndexingProviderAsync
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var registration = new ProviderRegistration(key, typeof(TSearchProvider), typeof(TIndexingProvider));
        if (Configuration.Providers.Any(existing => string.Equals(existing.Key, key, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"A search provider is already registered under the key '{key}'. Give each provider its own key.", nameof(key));
        }

        var sameProvider = Configuration.Providers.FirstOrDefault(existing =>
            existing.SearchProviderType == registration.SearchProviderType || existing.IndexingProviderType == registration.IndexingProviderType);
        if (sameProvider is not null)
        {
            throw new InvalidOperationException(
                $"{typeof(TSearchProvider).Name} is already registered under the key '{sameProvider.Key}'. Each provider can be added once per AddBetterSearch call; route indexes to other servers with its per-index settings.");
        }

        Configuration.Providers.Add(registration);
        return this;
    }

    /// <summary>
    /// Makes the provider registered under <paramref name="key"/> the one the unkeyed services use when more than
    /// one provider is registered. <c>AddBetterSearch</c> fails when no provider has that key.
    /// </summary>
    public Configurator SetDefaultProvider(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Configuration.DefaultProviderKey = key;
        return this;
    }

    /// <summary>
    /// Sends the unkeyed services' calls for <paramref name="index"/> to the provider registered under
    /// <paramref name="key"/>. Index names compare ignoring case, and a later call for the same index replaces the
    /// route. A route set here replaces one for the same index in the
    /// <see cref="SearchApplicationConfiguration.RoutesSectionKey"/> section. <c>AddBetterSearch</c> fails when no
    /// provider has that key.
    /// </summary>
    public Configurator RouteIndex(string index, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(index);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Configuration.RouteMap[index] = key;
        return this;
    }

    public Configurator SetDocumentValidatorType<T>() where T : class, IDocumentValidatorAsync
    {
        Configuration.DocumentValidatorType = typeof(T);
        return this;
    }

    public Configurator SetSearchServiceType<T>() where T : class, ISearchServiceAsync
    {
        Configuration.SearchServiceType = typeof(T);
        return this;
    }

    public Configurator SetIndexingServiceType<T>() where T : class, IIndexingServiceAsync
    {
        Configuration.IndexingServiceType = typeof(T);
        return this;
    }

    /// <summary>
    /// Sets the unkeyed search provider used when no provider was added with <see cref="AddProvider{TSearchProvider,TIndexingProvider}"/>.
    /// </summary>
    public Configurator SetSearchProviderType<T>() where T : class, ISearchProviderAsync
    {
        Configuration.SearchProviderType = typeof(T);
        return this;
    }

    /// <summary>
    /// Sets the unkeyed indexing provider used when no provider was added with <see cref="AddProvider{TSearchProvider,TIndexingProvider}"/>.
    /// </summary>
    public Configurator SetIndexingProviderType<T>() where T : class, IIndexingProviderAsync
    {
        Configuration.IndexingProviderType = typeof(T);
        return this;
    }
}
