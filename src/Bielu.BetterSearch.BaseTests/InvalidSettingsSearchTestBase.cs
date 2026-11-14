using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.DepedencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

/// <summary>
/// A search with missing or malformed connection settings returns a failed <c>Result</c> that names the setting,
/// rather than throwing. For providers that connect to a server, so not Lifti.
/// </summary>
public abstract class InvalidSettingsSearchTestBase
{
    /// <summary>The provider's configuration section, for example <c>Bielu:BetterSearch:ElasticSearch</c>.</summary>
    protected abstract string OptionsKey { get; }

    /// <summary>Calls the provider's extension, for example <c>search.AddElasticSearch()</c>.</summary>
    protected abstract void AddProvider(Configurator search);

    /// <summary>Searches the index <c>people</c> through <see cref="ISearchServiceAsync"/> with the settings given relative to <see cref="OptionsKey"/>.</summary>
    protected async Task<FluentResults.Result<ISearchResult>> SearchWithSettingsAsync(IDictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(setting => $"{OptionsKey}:{setting.Key}", setting => setting.Value))
            .Build();
        using var provider = new ServiceCollection().AddBetterSearch(configuration, AddProvider).BuildServiceProvider();
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISearchServiceAsync>()
            .SearchAsync(new SearchQuery { Index = "people" }, TestContext.Current.CancellationToken);
    }

    /// <summary>Pings through the registered <see cref="ISearchProviderAsync"/> with the settings given relative to <see cref="OptionsKey"/>.</summary>
    protected async Task<FluentResults.Result> PingWithSettingsAsync(IDictionary<string, string?> settings, string? index = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(setting => $"{OptionsKey}:{setting.Key}", setting => setting.Value))
            .Build();
        using var provider = new ServiceCollection().AddBetterSearch(configuration, AddProvider).BuildServiceProvider();
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISearchProviderAsync>()
            .PingAsync(index, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PingAsync_NoServerListening_ReturnsFailedResult()
    {
        // Port 1 is reserved (tcpmux) and nothing listens on it, so the connection is refused straight away.
        var result = await PingWithSettingsAsync(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "http://127.0.0.1:1",
        });

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("ping failed");
    }

    [Fact]
    public async Task PingAsync_InvalidIndexConnectionString_ReturnsFailedResultNamingTheIndexSetting()
    {
        var result = await PingWithSettingsAsync(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "http://localhost:9200",
            ["Indexes:People:ConnectionString"] = "localhost:9200",
        }, "people");

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("ConnectionString is not an http or https URL");
    }

    [Fact]
    public async Task SearchAsync_NoSettings_ReturnsFailedResultNamingConnectionString()
    {
        var result = await SearchWithSettingsAsync(new Dictionary<string, string?>());

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("ConnectionString is missing");
        result.Errors.Single().Message.ShouldContain($"{OptionsKey}:SharedIndexSettings:ConnectionString");
    }

    [Theory]
    [InlineData("  ", "ConnectionString is missing")]
    [InlineData("localhost:9200", "ConnectionString is not an http or https URL")]
    [InlineData("not a url", "ConnectionString is not an http or https URL")]
    [InlineData("ftp://localhost:9200", "ConnectionString is not an http or https URL")]
    public async Task SearchAsync_InvalidSharedConnectionString_ReturnsFailedResultNamingTheSetting(string connectionString, string expected)
    {
        var result = await SearchWithSettingsAsync(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = connectionString,
        });

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain(expected);
        result.Errors.Single().Message.ShouldContain($"{OptionsKey}:SharedIndexSettings:ConnectionString");
    }

    [Fact]
    public async Task SearchAsync_InvalidIndexConnectionString_ReturnsFailedResultNamingTheIndexSetting()
    {
        var result = await SearchWithSettingsAsync(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "http://localhost:9200",
            ["Indexes:People:ConnectionString"] = "localhost:9200",
        });

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("ConnectionString is not an http or https URL");
        result.Errors.Single().Message.ShouldContain($"{OptionsKey}:Indexes:<index>:ConnectionString");
    }
}
