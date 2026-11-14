using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.DepedencyInjection;
using Bielu.BetterSearch.OpenSearch.Services;
using Bielu.BetterSearch.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public class OpenSearchOptionsValidationTests : OptionsValidationTestBase
{
    private const string Key = "Bielu:BetterSearch:OpenSearch";

    protected override string OptionsKey => Key;

    protected override void AddProvider(Configurator search, string key) => search.AddOpenSearch(key);

    private static ServiceProvider Services(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection()
            .AddLogging()
            .AddBetterSearch(configuration, x => x.AddOpenSearch())
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
            [$"{Key}:SharedIndexSettings:ConnectionString"] = "http://localhost:9201",
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
        var options = new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings
            {
                Mappings =
                [
                    new OpenSearchFieldMapping { Field = "City", Type = OpenSearchFieldType.Keyword },
                    new OpenSearchFieldMapping { Field = "Tags", Type = OpenSearchFieldType.Keyword },
                ]
            },
            Indexes = new Dictionary<string, OpenSearchIndexSettings>
            {
                ["people"] = new() { Mappings = [new OpenSearchFieldMapping { Field = "City", Type = OpenSearchFieldType.Text }] }
            }
        };
        var manager = new OpenSearchClientSettingsManager(new TestOptionsMonitor<OpenSearchOptions>(options));

        var mappings = manager.GetConnectionSettings("People").Mappings!;

        mappings.Select(m => (m.Field, m.Type)).ShouldBe([("Tags", OpenSearchFieldType.Keyword), ("City", OpenSearchFieldType.Text)]);
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
    public void ValidateOnStart_IndexApiKeyAuthenticationWithoutKey_FailsNamingTheIndexSetting()
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "http://localhost:9200",
            ["Indexes:people:AuthenticationType"] = "APIKEY",
            ["Indexes:people:AuthenticationDetails:ApiKey"] = " ",
        });

        failure.ShouldNotBeNull().Failures.ShouldBe([
            $"APIKEY authentication needs AuthenticationDetails:ApiKey. Set {Key}:Indexes:people:AuthenticationDetails:ApiKey, or {Key}:SharedIndexSettings:AuthenticationDetails:ApiKey for every index."
        ]);
    }

    [Fact]
    public void ValidateOnStart_SharedPasswordWithoutDetailsAndBadUrl_ListsBothFailures()
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "localhost:9200",
            ["SharedIndexSettings:AuthenticationType"] = "PASSWORD",
        });

        failure.ShouldNotBeNull().Failures.ShouldBe([
            $"ConnectionString is not an http or https URL. Set {Key}:SharedIndexSettings:ConnectionString, or {Key}:Indexes:<index>:ConnectionString for one index.",
            $"PASSWORD authentication needs AuthenticationDetails. Set {Key}:SharedIndexSettings:AuthenticationDetails, or {Key}:Indexes:<index>:AuthenticationDetails for one index.",
        ]);
    }

    [Fact]
    public void ValidateOnStart_SharedPasswordWithoutUrlAndEveryIndexWithItsOwn_Passes()
    {
        ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:AuthenticationType"] = "PASSWORD",
            ["SharedIndexSettings:AuthenticationDetails:Username"] = "admin",
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
            ["SharedIndexSettings:AuthenticationDetails:Username"] = "admin",
            ["Indexes:people:ConnectionString"] = "http://people:9200",
        });

        failure.ShouldNotBeNull().Failures.ShouldBe([
            $"PASSWORD authentication needs AuthenticationDetails:Password. Set {Key}:Indexes:people:AuthenticationDetails:Password, or {Key}:SharedIndexSettings:AuthenticationDetails:Password for every index."
        ]);
    }

    [Fact]
    public void ValidateOnStart_ValidAdditionalNodesAndPoolType_Passes()
    {
        using var services = Services(new()
        {
            [$"{Key}:SharedIndexSettings:ConnectionString"] = "http://first:9200",
            [$"{Key}:SharedIndexSettings:AdditionalNodes:0"] = "http://second:9200",
            [$"{Key}:SharedIndexSettings:PoolType"] = "Sniffing",
            [$"{Key}:Indexes:people:AdditionalNodes:0"] = "http://people:9200",
        });

        Should.NotThrow(() => ValidateOnStart(services));
    }

    [Theory]
    [InlineData("first:9200", "AdditionalNodes:1 is not an http or https URL.")]
    [InlineData("ftp://first:9200", "AdditionalNodes:1 is not an http or https URL.")]
    [InlineData(" ", "AdditionalNodes:1 is missing.")]
    [InlineData("https://first:9200", "AdditionalNodes:1 does not use http, as ConnectionString does, and every node must use the same scheme.")]
    public void ValidateOnStart_InvalidSharedAdditionalNode_ThrowsNamingItsPosition(string node, string expected)
    {
        using var services = Services(new()
        {
            [$"{Key}:SharedIndexSettings:ConnectionString"] = "http://first:9200",
            [$"{Key}:SharedIndexSettings:AdditionalNodes:0"] = "http://second:9200",
            [$"{Key}:SharedIndexSettings:AdditionalNodes:1"] = node,
        });

        var exception = Should.Throw<OptionsValidationException>(() => ValidateOnStart(services));

        exception.Message.ShouldContain($"{expected} Set {Key}:SharedIndexSettings:AdditionalNodes:1, or {Key}:Indexes:<index>:AdditionalNodes:1 for one index.");
        exception.Message.ShouldNotContain("AdditionalNodes:0");
    }

    [Fact]
    public void ValidateOnStart_IndexWithOnlyAnInvalidAdditionalNode_ThrowsNamingTheIndexSetting()
    {
        using var services = Services(new()
        {
            [$"{Key}:SharedIndexSettings:ConnectionString"] = "http://first:9200",
            [$"{Key}:Indexes:people:AdditionalNodes:0"] = "people:9200",
        });

        var exception = Should.Throw<OptionsValidationException>(() => ValidateOnStart(services));

        exception.Message.ShouldContain($"AdditionalNodes:0 is not an http or https URL. Set {Key}:Indexes:people:AdditionalNodes:0, or {Key}:SharedIndexSettings:AdditionalNodes:0 for every index.");
    }

    [Fact]
    public void ValidateOnStart_IndexWithOnlyAPoolTypeAndNoConnectionStringAnywhere_ThrowsNamingTheIndexSetting()
    {
        using var services = Services(new()
        {
            [$"{Key}:Indexes:people:PoolType"] = "Sticky",
        });

        var exception = Should.Throw<OptionsValidationException>(() => ValidateOnStart(services));

        exception.Message.ShouldContain($"ConnectionString is missing. Set {Key}:Indexes:people:ConnectionString");
    }
}
