using Bielu.BetterSearch.Abstractions.Services.NullImplementations;
using Bielu.BetterSearch.Services;

namespace Bielu.BetterSearch.Configuration;

public class SearchApplicationConfiguration
{
    public Type DocumentValidatorType { get; set; } = typeof(DummyDocumentValidatorAsync);
    public Type IndexingServiceType { get; set; } = typeof(IndexingServiceAsync);

    public Type SearchServiceType { get; set; } = typeof(SearchServiceAsync);
    /// <summary>
    /// The indexing provider of the unkeyed services: the default provider's once <c>AddBetterSearch</c> has run, or
    /// <see cref="DummyIndexingProviderAsync"/> when no provider is registered or two or more are and none is the default.
    /// </summary>
    public Type IndexingProviderType { get; set; } = typeof(DummyIndexingProviderAsync);

    /// <summary>The search provider of the unkeyed services; see <see cref="IndexingProviderType"/>.</summary>
    public Type SearchProviderType { get; set; } = typeof(DummySearchProviderAsync);

    /// <summary>The key of the provider the unkeyed services use when more than one is registered.</summary>
    public string? DefaultProviderKey { get; set; }

    /// <summary>The keys of the providers added with <see cref="Configurator.AddProvider{TSearchProvider,TIndexingProvider}"/>, in the order they were added.</summary>
    public IReadOnlyList<string> ProviderKeys => Providers.Select(provider => provider.Key).ToList();

    /// <summary>The configuration section <c>AddBetterSearch</c> reads routes from: index name to provider key.</summary>
    public const string RoutesSectionKey = "Bielu:BetterSearch:Routes";

    /// <summary>
    /// Index name to provider key: the unkeyed services send a call for an index to the provider under its key.
    /// Index names compare ignoring case. Set with <see cref="Configurator.RouteIndex"/> or in the
    /// <see cref="RoutesSectionKey"/> section; once <c>AddBetterSearch</c> has run, this holds both.
    /// </summary>
    public IReadOnlyDictionary<string, string> Routes => RouteMap;

    internal List<ProviderRegistration> Providers { get; } = [];

    internal Dictionary<string, string> RouteMap { get; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed record ProviderRegistration(string Key, Type SearchProviderType, Type IndexingProviderType);
