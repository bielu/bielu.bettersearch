using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.PostgreSql.Configuration;
using Bielu.BetterSearch.PostgreSql.Services;
using Bielu.BetterSearch.PostgreSql.Services.Queries;
using Bielu.BetterSearch.Services;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bielu.BetterSearch.PostgreSql.DepedencyInjection;

public static class RegisterSearchExtension
{
    /// <summary>The key AddPostgreSql registers its provider under when no key is given.</summary>
    public const string DefaultKey = "PostgreSql";

    /// <summary>
    /// Adds the PostgreSQL provider under <paramref name="key"/>, so its services can be resolved with
    /// <c>[FromKeyedServices(key)]</c>; the unkeyed services use it when it is the only provider or the default.
    /// An index with no connection string uses the <see cref="NpgsqlDataSource"/> registered in the service collection.
    /// </summary>
    public static Configurator AddPostgreSql(this Configurator configurator, string key = DefaultKey)
    {
        // AddBetterSearch registers the validator and the indexing and search services after this runs.
        configurator.AddProvider<PostgreSqlSearchProviderAsync, PostgreSqlIndexingProviderAsync>(key);
        configurator.Services.AddSingleton<IPostgreSqlClientSettingsManager, PostgreSqlClientSettingsManager>();
        configurator.Services.AddSingleton<IClientFactoryAsync<NpgsqlDataSource>, PostgreSqlDataSourceFactory>();
        configurator.Services
            .AddScoped<IQueryTranslateServiceAsync<PostgreSqlSearchRequest>, PostgreSqlQueryTranslateService>();
        configurator.Services.AddScoped<ISubQueryTranslator<PostgreSqlFilter>, BooleanSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<PostgreSqlFilter>, TermSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<PostgreSqlFilter>, TermsSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<PostgreSqlFilter>, FuzzySubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<PostgreSqlFilter>, PrefixSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<PostgreSqlFilter>, PrefixPhraseSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<PostgreSqlFilter>, LuceneSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<PostgreSqlFilter>, NumericRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<PostgreSqlFilter>, LongRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<PostgreSqlFilter>, DateRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<PostgreSqlFilter>, StringRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<PostgreSqlFilter>, SpatialSearchSubQueryTranslator>();
        OptionsRegistration.BindAndValidateOnStart<PostgreSqlOptions, PostgreSqlOptionsValidator>(
            configurator.Services, configurator.AppConfiguration, PostgreSqlOptions.PostgreSqlOptionsKey);
        return configurator;
    }
}
