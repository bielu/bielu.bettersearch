using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.DepedencyInjection;
using Bielu.BetterSearch.ElasticSearch.Services;
using Bielu.BetterSearch.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public class ElasticSearchOptionsValidationTests : OptionsValidationTestBase
{
    private const string Key = "Bielu:BetterSearch:ElasticSearch";

    protected override string OptionsKey => Key;

    protected override void AddProvider(Configurator search, string key) => search.AddElasticSearch(key);

    private static ServiceProvider Services(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection()
            .AddLogging()
            .AddBetterSearch(configuration, x => x.AddElasticSearch())
            .BuildServiceProvider();
    }

    /// <summary>Runs the checks a host runs when it starts.</summary>
    private static void ValidateOnStart(ServiceProvider services) =>
        services.GetRequiredService<IStartupValidator>().Validate();

    [Fact]
    public void ValidateOnStart_ValidMappings_Passes()
    {
        using var services = Services(new()
        {
            [$"{Key}:SharedIndexSettings:ConnectionString"] = "http://localhost:9200",
            [$"{Key}:SharedIndexSettings:Mappings:0:Field"] = "City",
            [$"{Key}:SharedIndexSettings:Mappings:0:Type"] = "Keyword",
            [$"{Key}:Indexes:people:Mappings:0:Field"] = "Title",
            [$"{Key}:Indexes:people:Mappings:0:Type"] = "Text",
            [$"{Key}:Indexes:people:Mappings:0:Analyzer"] = "english",
            [$"{Key}:Indexes:people:Mappings:1:Field"] = "Born",
            [$"{Key}:Indexes:people:Mappings:1:Type"] = "Date",
            [$"{Key}:Indexes:people:Mappings:1:Format"] = "yyyy-MM-dd",
        });

        Should.NotThrow(() => ValidateOnStart(services));
    }

    [Theory]
    [InlineData("Field", "", "SharedIndexSettings:Mappings:0:Field is missing")]
    [InlineData("Field", "fields.City", "SharedIndexSettings:Mappings:0:Field fields.City starts with fields.")]
    [InlineData("Field", "City.", "SharedIndexSettings:Mappings:0:Field City. is not a valid field name")]
    [InlineData("Analyzer", "english", "SharedIndexSettings:Mappings:0:Analyzer is set, but only a non-empty analyzer on a Text field")]
    [InlineData("Format", "yyyy", "SharedIndexSettings:Mappings:0:Format is set, but only a non-empty format on a Date field")]
    public void ValidateOnStart_InvalidSharedMapping_ThrowsNamingTheSetting(string setting, string value, string expected)
    {
        var settings = new Dictionary<string, string?>
        {
            [$"{Key}:SharedIndexSettings:Mappings:0:Field"] = "City",
            [$"{Key}:SharedIndexSettings:Mappings:0:Type"] = "Keyword",
        };
        settings[$"{Key}:SharedIndexSettings:Mappings:0:{setting}"] = value;
        using var services = Services(settings);

        var exception = Should.Throw<OptionsValidationException>(() => ValidateOnStart(services));

        exception.Message.ShouldContain($"{Key}:{expected}");
    }

    [Fact]
    public void ValidateOnStart_IndexMappingWithoutType_ThrowsNamingTheIndexSetting()
    {
        using var services = Services(new()
        {
            [$"{Key}:Indexes:people:Mappings:0:Field"] = "City",
        });

        var exception = Should.Throw<OptionsValidationException>(() => ValidateOnStart(services));

        exception.Message.ShouldContain($"{Key}:Indexes:people:Mappings:0:Type is missing or unknown.");
    }

    [Theory]
    [InlineData("SharedIndexSettings", "0")]
    [InlineData("Indexes:people", "-5")]
    public void ValidateOnStart_BulkBatchSizeNotPositive_ThrowsNamingTheSetting(string path, string value)
    {
        using var services = Services(new()
        {
            [$"{Key}:{path}:BulkBatchSize"] = value,
        });

        var exception = Should.Throw<OptionsValidationException>(() => ValidateOnStart(services));

        exception.Message.ShouldContain($"{Key}:{path}:BulkBatchSize is {value}, but it must be greater than 0.");
    }

    [Fact]
    public void ValidateOnStart_FieldMappedTwiceInOneList_ThrowsNamingTheDuplicate()
    {
        using var services = Services(new()
        {
            [$"{Key}:SharedIndexSettings:Mappings:0:Field"] = "City",
            [$"{Key}:SharedIndexSettings:Mappings:0:Type"] = "Keyword",
            [$"{Key}:SharedIndexSettings:Mappings:1:Field"] = "City",
            [$"{Key}:SharedIndexSettings:Mappings:1:Type"] = "Text",
        });

        var exception = Should.Throw<OptionsValidationException>(() => ValidateOnStart(services));

        exception.Message.ShouldContain($"{Key}:SharedIndexSettings:Mappings:1:Field City is mapped more than once.");
    }

    [Fact]
    public void GetConnectionSettings_IndexMapsASharedField_IndexMappingWinsAndSharedOnesStay()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings
            {
                Mappings =
                [
                    new ElasticSearchFieldMapping { Field = "City", Type = ElasticSearchFieldType.Keyword },
                    new ElasticSearchFieldMapping { Field = "Tags", Type = ElasticSearchFieldType.Keyword },
                ]
            },
            Indexes = new Dictionary<string, ElasticSearchIndexSettings>
            {
                ["people"] = new() { Mappings = [new ElasticSearchFieldMapping { Field = "City", Type = ElasticSearchFieldType.Text }] }
            }
        };
        var manager = new ElasticSearchClientSettingsManager(new TestOptionsMonitor<ElasticSearchOptions>(options));

        var mappings = manager.GetConnectionSettings("People").Mappings!;

        mappings.Select(m => (m.Field, m.Type)).ShouldBe([("Tags", ElasticSearchFieldType.Keyword), ("City", ElasticSearchFieldType.Text)]);
        options.SharedIndexSettings.Mappings!.Count.ShouldBe(2);
        manager.GetConnectionSettings("other").Mappings!.Select(m => m.Field).ShouldBe(["City", "Tags"]);
    }

    [Fact]
    public void ValidateOnStart_ValidIndexPrefix_Passes()
    {
        using var services = Services(new()
        {
            [$"{Key}:SharedIndexSettings:ConnectionString"] = "http://localhost:9200",
            [$"{Key}:IndexPrefix"] = "app-1.",
        });

        Should.NotThrow(() => ValidateOnStart(services));
    }

    [Theory]
    [InlineData("App-", "has upper case letters")]
    [InlineData("app/", "has a character index names can't contain")]
    [InlineData("app ", "has a character index names can't contain")]
    [InlineData("_app", "starts with '_'")]
    [InlineData("-app", "starts with '-'")]
    public void ValidateOnStart_InvalidIndexPrefix_ThrowsNamingTheSetting(string prefix, string expected)
    {
        using var services = Services(new() { [$"{Key}:IndexPrefix"] = prefix });

        var exception = Should.Throw<OptionsValidationException>(() => ValidateOnStart(services));

        exception.Message.ShouldContain($"{Key}:IndexPrefix '{prefix}' {expected}");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void ValidateOnStart_MaxResultWindowNotPositive_FailsNamingTheSetting(string value)
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "http://localhost:9200",
            ["MaxResultWindow"] = value,
        });

        failure.ShouldNotBeNull().Failures.ShouldBe([$"{Key}:MaxResultWindow is {value}, but it must be greater than 0."]);
    }

    [Fact]
    public void ValidateOnStart_IndexPasswordAuthenticationWithoutPassword_FailsNamingTheIndexSetting()
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "http://localhost:9200",
            ["Indexes:people:AuthenticationType"] = "PASSWORD",
            ["Indexes:people:AuthenticationDetails:Username"] = "elastic",
            ["Indexes:people:AuthenticationDetails:Password"] = "",
        });

        failure.ShouldNotBeNull().Failures.ShouldBe([
            $"PASSWORD authentication needs AuthenticationDetails:Password. Set {Key}:Indexes:people:AuthenticationDetails:Password."
        ]);
    }

    [Fact]
    public void ValidateOnStart_SharedCloudApiWithoutKey_FailsAndListsEveryOtherFailure()
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:AuthenticationType"] = "CLOUDAPI",
            ["SharedIndexSettings:AuthenticationDetails:Id"] = "name:abc",
            ["MaxResultWindow"] = "0",
        });

        failure.ShouldNotBeNull().Failures.ShouldBe([
            $"CLOUDAPI authentication needs AuthenticationDetails:ApiKey. Set {Key}:SharedIndexSettings:AuthenticationDetails:ApiKey, or {Key}:Indexes:<index>:AuthenticationType with {Key}:Indexes:<index>:AuthenticationDetails for one index.",
            $"{Key}:MaxResultWindow is 0, but it must be greater than 0.",
        ]);
    }

    [Fact]
    public void ValidateOnStart_CloudWithoutConnectionString_Passes()
    {
        ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:AuthenticationType"] = "CLOUD",
            ["SharedIndexSettings:AuthenticationDetails:Id"] = "name:abc",
            ["SharedIndexSettings:AuthenticationDetails:Username"] = "elastic",
            ["SharedIndexSettings:AuthenticationDetails:Password"] = "secret",
        }).ShouldBeNull();
    }

    [Fact]
    public void ValidateOnStart_UnknownAuthenticationType_FailsListingTheTypes()
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "http://localhost:9200",
            ["SharedIndexSettings:AuthenticationType"] = "7",
        });

        failure.ShouldNotBeNull().Failures.ShouldBe([
            $"AuthenticationType 7 is not one of NONE, CLOUD, CLOUDAPI, PASSWORD. Set {Key}:SharedIndexSettings:AuthenticationType, or {Key}:Indexes:<index>:AuthenticationType for one index."
        ]);
    }

    [Fact]
    public void ValidateOnStart_SharedPasswordWithoutUrlAndEveryIndexWithItsOwn_Passes()
    {
        ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:AuthenticationType"] = "PASSWORD",
            ["SharedIndexSettings:AuthenticationDetails:Username"] = "elastic",
            ["SharedIndexSettings:AuthenticationDetails:Password"] = "secret",
            ["Indexes:people:ConnectionString"] = "http://people:9200",
            ["Indexes:places:ConnectionString"] = "https://places:9200",
        }).ShouldBeNull();
    }

    [Fact]
    public void ValidateOnStart_SharedPasswordWithoutUrlOrPassword_FailsNamingEachIndexThatInheritsIt()
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:AuthenticationType"] = "PASSWORD",
            ["SharedIndexSettings:AuthenticationDetails:Username"] = "elastic",
            ["Indexes:people:ConnectionString"] = "http://people:9200",
        });

        failure.ShouldNotBeNull().Failures.ShouldBe([
            $"PASSWORD authentication needs AuthenticationDetails:Password. Set {Key}:SharedIndexSettings:AuthenticationDetails:Password, which {Key}:Indexes:people inherits."
        ]);
    }

    [Theory]
    [InlineData("SharedIndexSettings", "RequestTimeout", "00:00:00", "must be greater than 0.")]
    [InlineData("Indexes:people", "RequestTimeout", "-00:00:01", "must be greater than 0.")]
    [InlineData("SharedIndexSettings", "RequestTimeout", "30.00:00:00", "must be at most 24.20:31:23.6470000, the longest timeout HttpClient accepts.")]
    [InlineData("Indexes:people", "RequestTimeout", "24.20:31:23.6480000", "must be at most 24.20:31:23.6470000, the longest timeout HttpClient accepts.")]
    [InlineData("SharedIndexSettings", "MaxRetries", "-1", "must not be negative.")]
    [InlineData("Indexes:people", "MaxRetries", "-2", "must not be negative.")]
    public void ValidateOnStart_InvalidClientSetting_ThrowsNamingTheSetting(string path, string setting, string value, string rule)
    {
        using var services = Services(new()
        {
            [$"{Key}:SharedIndexSettings:ConnectionString"] = "http://localhost:9200",
            [$"{Key}:{path}:{setting}"] = value,
        });

        var exception = Should.Throw<OptionsValidationException>(() => ValidateOnStart(services));

        exception.Failures.ShouldBe([$"{Key}:{path}:{setting} is {value}, but it {rule}"]);
    }

    [Fact]
    public void ValidateOnStart_ValidClientSettings_Passes()
    {
        using var services = Services(new()
        {
            [$"{Key}:SharedIndexSettings:ConnectionString"] = "http://localhost:9200",
            [$"{Key}:SharedIndexSettings:RequestTimeout"] = "00:00:05",
            [$"{Key}:SharedIndexSettings:MaxRetries"] = "0",
            [$"{Key}:Indexes:people:RequestTimeout"] = "24.20:31:23.6470000",
        });

        Should.NotThrow(() => ValidateOnStart(services));
    }

    [Fact]
    public void ValidateOnStart_IndexWithOwnAuthenticationInheritingMissingConnectionString_FailsNamingOnlyTheSharedSetting()
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>
        {
            ["Indexes:people:AuthenticationType"] = "PASSWORD",
            ["Indexes:people:AuthenticationDetails:Username"] = "elastic",
            ["Indexes:people:AuthenticationDetails:Password"] = "secret",
        });

        failure.ShouldNotBeNull().Failures.ShouldBe([
            $"ConnectionString is missing. Set {Key}:SharedIndexSettings:ConnectionString, which {Key}:Indexes:people inherits."
        ]);
    }

    [Fact]
    public void ValidateOnStart_IndexWithOwnConnectionStringAndUnknownAuthenticationType_FailsNamingOnlyTheIndexSetting()
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:AuthenticationType"] = "PASSWORD",
            ["SharedIndexSettings:AuthenticationDetails:Username"] = "elastic",
            ["SharedIndexSettings:AuthenticationDetails:Password"] = "secret",
            ["Indexes:people:ConnectionString"] = "http://people:9200",
            ["Indexes:people:AuthenticationType"] = "7",
        });

        failure.ShouldNotBeNull().Failures.ShouldHaveSingleItem().ShouldEndWith($"Set {Key}:Indexes:people:AuthenticationType.");
    }
}
