using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Meilisearch.DepedencyInjection;
using Bielu.BetterSearch.Meilisearch.Services;
using Bielu.BetterSearch.Tests;
using Shouldly;

namespace Bielu.BetterSearch.Meilisearch.Tests;

public class MeilisearchOptionsValidationTests : OptionsValidationTestBase
{
    private const string Key = "Bielu:BetterSearch:Meilisearch";

    protected override string OptionsKey => Key;

    protected override void AddProvider(Configurator search, string key) => search.AddMeilisearch(key);

    private OptionsValidationFailures Validate(string setting, string value) => new(ValidateOnStart(new Dictionary<string, string?>
    {
        ["SharedIndexSettings:ConnectionString"] = "http://localhost:7700",
        [setting] = value,
    })?.Failures.ToList());

    private sealed record OptionsValidationFailures(List<string>? Failures);

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void ValidateOnStart_MaxTotalHitsNotPositive_FailsNamingTheSetting(string value)
    {
        Validate("MaxTotalHits", value).Failures.ShouldBe([$"{Key}:MaxTotalHits is {value}, but it must be greater than 0."]);
    }

    [Theory]
    [InlineData("TaskTimeout")]
    [InlineData("SettingsCheckInterval")]
    public void ValidateOnStart_IntervalNegative_FailsNamingTheSetting(string setting)
    {
        Validate(setting, "-00:00:01").Failures.ShouldBe([$"{Key}:{setting} is -00:00:01, but it must not be negative."]);
    }

    [Fact]
    public void ValidateOnStart_SettingsCheckIntervalZero_Passes()
    {
        Validate("SettingsCheckInterval", "00:00:00").Failures.ShouldBeNull();
    }

    [Fact]
    public void ValidateOnStart_GeoFieldWithASpace_FailsNamingTheSetting()
    {
        Validate("GeoField", "home location").Failures.ShouldBe([
            $"{Key}:GeoField home location can only contain letters, digits, '_', '-' and '.', as Meilisearch filters write field names unquoted."
        ]);
    }

    [Fact]
    public void ValidateOnStart_PhraseFieldsEntryInvalid_FailsNamingTheEntry()
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "http://localhost:7700",
            ["PhraseFields:0"] = "Title",
            ["PhraseFields:1"] = "Body(text)",
        });

        failure.ShouldNotBeNull().Failures.ShouldBe([
            $"{Key}:PhraseFields:1 Body(text) can only contain letters, digits, '_', '-' and '.', as Meilisearch filters write field names unquoted."
        ]);
    }

    [Fact]
    public void ValidateOnStart_ProviderAddedUnderTwoKeysWithOneConfiguration_BindsItsListsOnce()
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "http://localhost:7700",
            ["PhraseFields:0"] = "Body(text)",
        }, "first", "second");

        failure.ShouldNotBeNull().Failures.ShouldBe([
            $"{Key}:PhraseFields:0 Body(text) can only contain letters, digits, '_', '-' and '.', as Meilisearch filters write field names unquoted."
        ]);
    }

    [Fact]
    public void ValidateOnStart_ValidFieldNames_Passes()
    {
        ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "http://localhost:7700",
            ["GeoField"] = "Location",
            ["PhraseFields:0"] = "Title",
            ["PhraseFields:1"] = "fields.Body-text_2",
        }).ShouldBeNull();
    }

    [Fact]
    public async Task SearchAsync_MaxTotalHitsNotPositiveOnAMonitorThatDoesNotValidate_ReturnsFailedResultNamingTheSetting()
    {
        var options = new MeilisearchOptions
        {
            SharedIndexSettings = new MeilisearchIndexSettings { ConnectionString = "http://localhost:7700" },
            MaxTotalHits = 0,
        };
        var monitor = new StaticOptionsMonitor(options);
        var provider = new MeilisearchSearchProviderAsync(null!,
            new MeilisearchClientFactory(new MeilisearchClientSettingsManager(monitor)), monitor);

        var result = await provider.SearchAsync(new SearchQuery { Index = "people" }, TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().Message.ShouldBe($"{Key}:MaxTotalHits is 0, but it must be greater than 0.");
    }
}
