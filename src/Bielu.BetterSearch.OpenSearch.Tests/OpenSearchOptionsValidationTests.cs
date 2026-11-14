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
        var manager = new OpenSearchClientSettingsManager(new StaticOptions(options));

        var mappings = manager.GetConnectionSettings("People").Mappings!;

        mappings.Select(m => (m.Field, m.Type)).ShouldBe([("Tags", OpenSearchFieldType.Keyword), ("City", OpenSearchFieldType.Text)]);
        options.SharedIndexSettings.Mappings!.Count.ShouldBe(2);
        manager.GetConnectionSettings("other").Mappings!.Select(m => m.Field).ShouldBe(["City", "Tags"]);
    }

    private sealed class StaticOptions(OpenSearchOptions value) : IOptionsMonitor<OpenSearchOptions>
    {
        public OpenSearchOptions CurrentValue => value;
        public OpenSearchOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<OpenSearchOptions, string?> listener) => null;
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
}
