using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.Services;
using Bielu.BetterSearch.OpenSearch.Services.Queries;
using Bielu.BetterSearch.Services;
using Microsoft.Extensions.DependencyInjection;
using OpenSearch.Client;
using OpenSearch.Net;

namespace Bielu.BetterSearch.OpenSearch.DependencyInjection;

public static class RegisterSearchExtension
{
    /// <summary>The key AddOpenSearch registers its provider under when no key is given.</summary>
    public const string DefaultKey = "OpenSearch";

    /// <summary>
    /// Adds the OpenSearch provider under <paramref name="key"/>, so its services can be resolved with
    /// <c>[FromKeyedServices(key)]</c>; the unkeyed services use it when it is the only provider or the default.
    /// <c>configureClient</c> sets what the options don't cover, such as a proxy or a header, on the <c>ConnectionSettings</c> of each client
    /// the provider creates. It runs after the options are applied, so it can override them, and once per cached client,
    /// not per request: clients are cached per connection. It applies to every OpenSearch client, whichever key routes to it.
    /// </summary>
    public static Configurator AddOpenSearch(this Configurator configurator, string key = DefaultKey,
        Action<ConnectionSettings>? configureClient = null)
    {
        // AddBetterSearch registers the validator and the indexing and search services after this runs.
        configurator.AddProvider<OpenSearchSearchProviderAsync, OpenSearchIndexingProviderAsync>(key);
        configurator.Services.AddSingleton<IOpenSearchClientSettingsManager, OpenSearchClientSettingsManager>();
        ClientConfiguration<ConnectionSettings>.Register(configurator.Services, configureClient);
        configurator.Services.AddSingleton<IClientFactoryAsync<IOpenSearchClient>>(services => new OpenSearchClientFactory(
            services.GetRequiredService<IOpenSearchClientSettingsManager>(),
            services.GetServices<ClientConfiguration<ConnectionSettings>>(),
            services.GetServices<OpenSearchConnectionSource>().LastOrDefault()?.Bind(services)));
        configurator.Services.AddScoped<OpenSearchQueryTranslateService>();
        configurator.Services.AddScoped<IQueryTranslateServiceAsync<QueryContainer>>(sp =>
            sp.GetRequiredService<OpenSearchQueryTranslateService>());
        configurator.Services.AddScoped<IPostFilterTranslateServiceAsync<QueryContainer>>(sp =>
            sp.GetRequiredService<OpenSearchQueryTranslateService>());
        configurator.Services.AddScoped<IResultMapper<ISearchResponse<SearchDocument>>, OpenSearchResultMapper>();

        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, BooleanSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, TermSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, TermsSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, FuzzySubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, PrefixSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, PrefixPhraseSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, LuceneSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, NumericRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, LongRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, DateRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, StringRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, SpatialSearchSubQueryTranslator>();
        OptionsRegistration.BindAndValidateOnStart<OpenSearchOptions, OpenSearchOptionsValidator>(
            configurator.Services, configurator.AppConfiguration, OpenSearchOptions.OpenSearchOptionsKey);
        return configurator;
    }

    /// <summary>
    /// Sends the requests of every OpenSearch client the provider creates through the connection
    /// <paramref name="createConnection"/> returns, such as one that signs each request, instead of the client's default
    /// <c>HttpConnection</c>. It runs when a client is created, not per request; two calls racing to create the same client
    /// can each run it, and only one client and its connection are kept. The other connection is dropped without being
    /// disposed, so a connection should take disposable resources on its first request, not when it is created. The
    /// options still apply, so a connection that sets its own <c>Authorization</c> header needs <c>AuthenticationType</c>
    /// left at <c>None</c>. Called more than once, the last call wins. It can be called before or after <c>AddOpenSearch</c>.
    /// </summary>
    public static Configurator UseOpenSearchConnection(this Configurator configurator,
        Func<IServiceProvider, IConnection> createConnection)
    {
        ArgumentNullException.ThrowIfNull(createConnection);
        configurator.Services.AddSingleton(new OpenSearchConnectionSource(createConnection));
        return configurator;
    }
}
