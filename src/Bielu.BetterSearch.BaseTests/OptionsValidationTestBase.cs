using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.DepedencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

/// <summary>
/// The connection setting checks every provider that connects to a server runs at startup, through the shared
/// <c>ConnectionSettingsChecks</c>: every failure is listed at once, each naming its configuration path.
/// </summary>
public abstract class OptionsValidationTestBase
{
    /// <summary>The provider's configuration section, for example <c>Bielu:BetterSearch:ElasticSearch</c>.</summary>
    protected abstract string OptionsKey { get; }

    /// <summary>Calls the provider's extension with a key, for example <c>search.AddElasticSearch(key)</c>.</summary>
    protected abstract void AddProvider(Configurator search, string key);

    /// <summary>
    /// Runs the checks a host runs when it starts, with the settings given relative to <see cref="OptionsKey"/>, and
    /// returns the validation failure, or <c>null</c> when the settings pass. Each of <paramref name="keys"/> adds the
    /// provider under that key in its own <c>AddBetterSearch</c> call.
    /// </summary>
    protected OptionsValidationException? ValidateOnStart(IDictionary<string, string?> settings, params string[] keys)
    {
        var configuration = Configuration(settings);
        var services = new ServiceCollection();
        foreach (var key in keys.Length == 0 ? ["default"] : keys)
        {
            services.AddBetterSearch(configuration, search => AddProvider(search, key));
        }

        return ValidateOnStart(services);
    }

    /// <summary>
    /// Like <see cref="ValidateOnStart(IDictionary{string, string?}, string[])"/>, but each AddBetterSearch call gets its own
    /// configuration, in order, from <paramref name="settingsPerCall"/>. A <c>null</c> entry calls the overload that takes
    /// no configuration.
    /// </summary>
    protected OptionsValidationException? ValidateOnStartWithConfigurations(params IDictionary<string, string?>?[] settingsPerCall)
    {
        var services = new ServiceCollection();
        for (var call = 0; call < settingsPerCall.Length; call++)
        {
            var key = $"key{call}";
            if (settingsPerCall[call] is { } settings)
            {
                services.AddBetterSearch(Configuration(settings), search => AddProvider(search, key));
            }
            else
            {
                services.AddBetterSearch(search => AddProvider(search, key));
            }
        }

        return ValidateOnStart(services);
    }

    private IConfiguration Configuration(IDictionary<string, string?> settings) => new ConfigurationBuilder()
        .AddInMemoryCollection(settings.ToDictionary(setting => $"{OptionsKey}:{setting.Key}", setting => setting.Value))
        .Build();

    private static OptionsValidationException? ValidateOnStart(ServiceCollection services)
    {
        using var provider = services.BuildServiceProvider();
        try
        {
            provider.GetRequiredService<IStartupValidator>().Validate();
            return null;
        }
        catch (OptionsValidationException e)
        {
            return e;
        }
    }

    [Fact]
    public void ValidateOnStart_ValidSharedConnectionString_Passes()
    {
        ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "http://localhost:9200",
        }).ShouldBeNull();
    }

    [Fact]
    public void ValidateOnStart_NoSection_FailsOnceNamingTheSection()
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>());

        failure.ShouldNotBeNull().Failures.ShouldBe([
            $"No {OptionsKey} settings were found. Add the {OptionsKey} configuration section, with at least {OptionsKey}:SharedIndexSettings:ConnectionString."
        ]);
    }

    [Fact]
    public void ValidateOnStart_ProviderAddedUnderTwoKeys_ValidatesItsOptionsOnce()
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>(), "first", "second");

        failure.ShouldNotBeNull().Failures.ShouldHaveSingleItem().ShouldStartWith($"No {OptionsKey} settings were found.");
    }

    [Fact]
    public void ValidateOnStart_LaterCallWithAnotherConfiguration_BindsItOverTheFirst()
    {
        var failure = ValidateOnStartWithConfigurations(
            new Dictionary<string, string?> { ["SharedIndexSettings:ConnectionString"] = "http://localhost:9200" },
            new Dictionary<string, string?> { ["SharedIndexSettings:ConnectionString"] = "localhost:9200" });

        failure.ShouldNotBeNull().Failures.ShouldBe([
            $"ConnectionString is not an http or https URL. Set {OptionsKey}:SharedIndexSettings:ConnectionString, or {OptionsKey}:Indexes:<index>:ConnectionString for one index."
        ]);
    }

    [Fact]
    public void ValidateOnStart_FirstCallWithoutConfiguration_BindsTheLaterConfiguration()
    {
        ValidateOnStartWithConfigurations(
            null,
            new Dictionary<string, string?> { ["SharedIndexSettings:ConnectionString"] = "http://localhost:9200" }).ShouldBeNull();
    }

    [Theory]
    [InlineData("localhost:9200")]
    [InlineData("ftp://localhost:9200")]
    public void ValidateOnStart_SharedConnectionStringNotHttp_FailsNamingTheSetting(string connectionString)
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = connectionString,
        });

        failure.ShouldNotBeNull().Failures.ShouldBe([
            $"ConnectionString is not an http or https URL. Set {OptionsKey}:SharedIndexSettings:ConnectionString, or {OptionsKey}:Indexes:<index>:ConnectionString for one index."
        ]);
        failure.Message.ShouldNotContain(connectionString);
    }

    [Fact]
    public void ValidateOnStart_IndexConnectionStringNotHttp_FailsNamingTheIndexSetting()
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "http://localhost:9200",
            ["Indexes:people:ConnectionString"] = "localhost:9200",
        });

        failure.ShouldNotBeNull().Failures.ShouldBe([
            $"ConnectionString is not an http or https URL. Set {OptionsKey}:Indexes:people:ConnectionString, or {OptionsKey}:SharedIndexSettings:ConnectionString for every index."
        ]);
    }

    [Fact]
    public void ValidateOnStart_SeveralInvalidConnectionStrings_ListsEveryOneNamingItsPath()
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "localhost:9200",
            ["Indexes:people:ConnectionString"] = "people:9200",
            ["Indexes:places:ConnectionString"] = "not a url",
        });

        var failures = failure.ShouldNotBeNull().Failures.ToList();
        failures.Count.ShouldBe(3);
        failures.ShouldContain(f => f.Contains($"Set {OptionsKey}:SharedIndexSettings:ConnectionString,"));
        failures.ShouldContain(f => f.Contains($"Set {OptionsKey}:Indexes:people:ConnectionString,"));
        failures.ShouldContain(f => f.Contains($"Set {OptionsKey}:Indexes:places:ConnectionString,"));
    }

    [Fact]
    public void ValidateOnStart_IndexWithoutItsOwnConnection_ReportsTheSharedSettingOnce()
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>
        {
            ["Indexes:people:ConnectionString"] = "http://localhost:9200",
            ["Indexes:places:IndexName"] = "places",
        });

        failure.ShouldNotBeNull().Failures.ShouldBe([
            $"ConnectionString is missing. Set {OptionsKey}:SharedIndexSettings:ConnectionString, or {OptionsKey}:Indexes:<index>:ConnectionString for one index."
        ]);
    }

    [Fact]
    public void ValidateOnStart_EveryIndexHasItsOwnConnectionString_Passes()
    {
        ValidateOnStart(new Dictionary<string, string?>
        {
            ["Indexes:people:ConnectionString"] = "http://people:9200",
            ["Indexes:places:ConnectionString"] = "https://places:9200",
        }).ShouldBeNull();
    }
}
