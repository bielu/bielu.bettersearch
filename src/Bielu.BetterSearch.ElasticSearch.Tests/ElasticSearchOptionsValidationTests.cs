using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.DepedencyInjection;
using Bielu.BetterSearch.ElasticSearch.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public class ElasticSearchOptionsValidationTests
{
    private const string Key = "Bielu:BetterSearch:ElasticSearch";

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
        var manager = new ElasticSearchClientSettingsManager(new FixedOptionsMonitor(options));

        var mappings = manager.GetConnectionSettings("People").Mappings!;

        mappings.Select(m => (m.Field, m.Type)).ShouldBe([("Tags", ElasticSearchFieldType.Keyword), ("City", ElasticSearchFieldType.Text)]);
        options.SharedIndexSettings.Mappings!.Count.ShouldBe(2);
        manager.GetConnectionSettings("other").Mappings!.Select(m => m.Field).ShouldBe(["City", "Tags"]);
    }

    [Fact]
    public void ValidateOnStart_ValidIndexPrefix_Passes()
    {
        using var services = Services(new() { [$"{Key}:IndexPrefix"] = "app-1." });

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
}
