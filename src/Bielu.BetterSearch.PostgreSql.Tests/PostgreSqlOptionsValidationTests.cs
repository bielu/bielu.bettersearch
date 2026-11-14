using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.PostgreSql.DepedencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Shouldly;

namespace Bielu.BetterSearch.PostgreSql.Tests;

public class PostgreSqlOptionsValidationTests
{
    private const string Key = "Bielu:BetterSearch:PostgreSql";
    private const string Valid = "Host=localhost;Database=search;Username=app;Password=secret-value";

    private static ServiceProvider Services(IDictionary<string, string?> settings, Action<IServiceCollection>? configure = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(setting => $"{Key}:{setting.Key}", setting => setting.Value))
            .Build();
        var services = new ServiceCollection().AddLogging();
        configure?.Invoke(services);
        return services.AddBetterSearch(configuration, x => x.AddPostgreSql()).BuildServiceProvider();
    }

    private static IReadOnlyList<string>? ValidateOnStart(IDictionary<string, string?> settings, Action<IServiceCollection>? configure = null)
    {
        using var services = Services(settings, configure);
        try
        {
            services.GetRequiredService<IStartupValidator>().Validate();
            return null;
        }
        catch (OptionsValidationException e)
        {
            return e.Failures.ToList();
        }
    }

    [Fact]
    public void ValidateOnStart_ValidConnectionString_Passes()
    {
        ValidateOnStart(new Dictionary<string, string?> { ["SharedIndexSettings:ConnectionString"] = Valid }).ShouldBeNull();
    }

    [Fact]
    public void ValidateOnStart_NoSettings_FailsNamingTheSection()
    {
        ValidateOnStart(new Dictionary<string, string?>()).ShouldBe([
            $"No {Key} settings were found. Add the {Key} configuration section, with at least {Key}:SharedIndexSettings:ConnectionString."]);
    }

    [Fact]
    public void ValidateOnStart_NoConnectionStringButARegisteredDataSource_Passes()
    {
        ValidateOnStart(new Dictionary<string, string?>(),
            services => services.AddSingleton(_ => NpgsqlDataSource.Create(Valid))).ShouldBeNull();
    }

    [Theory]
    [InlineData("Host=localhost;NotAKeyword=1", "ConnectionString is not a valid Npgsql connection string.")]
    [InlineData("Database=search;Username=app", "ConnectionString names no Host.")]
    public void ValidateOnStart_InvalidSharedConnectionString_FailsNamingTheSettingButNotTheValue(string connectionString, string expected)
    {
        var failure = ValidateOnStart(new Dictionary<string, string?> { ["SharedIndexSettings:ConnectionString"] = connectionString })
            .ShouldNotBeNull().ShouldHaveSingleItem();

        failure.ShouldStartWith(expected);
        failure.ShouldContain($"Set {Key}:SharedIndexSettings:ConnectionString,");
        failure.ShouldNotContain(connectionString);
    }

    [Fact]
    public void ValidateOnStart_InvalidIndexConnectionString_FailsNamingTheIndexSetting()
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = Valid,
            ["Indexes:People:ConnectionString"] = "Password=secret-value",
        }).ShouldNotBeNull().ShouldHaveSingleItem();

        failure.ShouldBe($"ConnectionString names no Host. Set {Key}:Indexes:People:ConnectionString, or {Key}:SharedIndexSettings:ConnectionString for every index.");
    }

    [Fact]
    public void ValidateOnStart_IndexWithoutConnectionStringAndNoSharedOne_FailsNamingTheSharedSetting()
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>
        {
            ["Indexes:People:ConnectionString"] = Valid,
            ["Indexes:Places:IndexName"] = "places",
        }).ShouldNotBeNull().ShouldHaveSingleItem();

        failure.ShouldStartWith("ConnectionString is missing, and no NpgsqlDataSource is registered.");
        failure.ShouldContain($"Set {Key}:SharedIndexSettings:ConnectionString,");
    }

    [Theory]
    [InlineData("english'); DROP TABLE people; --")]
    [InlineData("pg catalog.english")]
    [InlineData("")]
    public void ValidateOnStart_TextSearchConfigurationNotAName_FailsNamingTheSetting(string configuration)
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = Valid,
            ["TextSearchConfiguration"] = configuration,
        }).ShouldNotBeNull().ShouldHaveSingleItem();

        failure.ShouldStartWith($"{Key}:TextSearchConfiguration '{configuration}' is not a text search configuration name");
    }

    [Theory]
    [InlineData("english")]
    [InlineData("pg_catalog.english")]
    public void ValidateOnStart_TextSearchConfigurationName_Passes(string configuration)
    {
        ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = Valid,
            ["TextSearchConfiguration"] = configuration,
        }).ShouldBeNull();
    }

    [Fact]
    public void ValidateOnStart_SchemaLongerThanPostgreSqlKeeps_FailsNamingTheSetting()
    {
        ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = Valid,
            ["Schema"] = new string('s', 64),
        }).ShouldBe([$"{Key}:Schema is longer than 63 bytes, the longest name PostgreSQL keeps."]);
    }

    [Fact]
    public async Task Providers_ReloadMakesTheConnectionStringInvalid_ReturnFailedResultsNamingTheSetting()
    {
        // Nothing listens on port 1, so a call that got past the options would fail with a connection error instead.
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{Key}:SharedIndexSettings:ConnectionString"] = "Host=127.0.0.1;Port=1",
        }).Build();
        using var services = new ServiceCollection()
            .AddLogging()
            .AddBetterSearch(configuration, x => x.AddPostgreSql())
            .BuildServiceProvider();
        services.GetRequiredService<IStartupValidator>().Validate();
        configuration[$"{Key}:TextSearchConfiguration"] = "not a name";
        Should.Throw<AggregateException>(configuration.Reload).InnerException.ShouldBeOfType<OptionsValidationException>();
        using var scope = services.CreateScope();
        var indexing = scope.ServiceProvider.GetRequiredService<IIndexingProviderAsync>();
        var search = scope.ServiceProvider.GetRequiredService<ISearchProviderAsync>();
        var token = TestContext.Current.CancellationToken;
        var document = new SearchDocument { Id = "1", Type = "TestDocument", Index = "people" };
        const string setting = $"{Key}:TextSearchConfiguration 'not a name' is not a text search configuration name";

        var results = new FluentResults.ResultBase[]
        {
            await indexing.IndexMultipleDocumentsAsync([document], token),
            await indexing.RemoveDocumentsAsync("people", ["1"], token),
            await indexing.CreateIndexAsync("people", token),
            await search.SearchAsync(new SearchQuery { Index = "people" }, token),
            await search.CountAsync(new SearchQuery { Index = "people" }, token),
        };

        foreach (var result in results)
        {
            result.IsFailed.ShouldBeTrue();
            result.Errors.Single().Message.ShouldContain(setting);
        }
    }
}
