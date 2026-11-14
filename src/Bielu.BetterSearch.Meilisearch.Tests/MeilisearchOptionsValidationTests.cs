using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Meilisearch.DepedencyInjection;
using Bielu.BetterSearch.Meilisearch.Services;
using Bielu.BetterSearch.Meilisearch.Services.Queries;
using Bielu.BetterSearch.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
    public void ValidateOnStart_PositiveBulkBatchSizes_Passes()
    {
        ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "http://localhost:7700",
            ["SharedIndexSettings:BulkBatchSize"] = "500",
            ["Indexes:people:BulkBatchSize"] = "1",
        }).ShouldBeNull();
    }

    [Theory]
    [InlineData("SharedIndexSettings", "0")]
    [InlineData("Indexes:people", "-5")]
    public void ValidateOnStart_BulkBatchSizeNotPositive_FailsNamingTheSetting(string path, string value)
    {
        Validate($"{path}:BulkBatchSize", value).Failures.ShouldBe([
            $"{Key}:{path}:BulkBatchSize is {value}, but it must be greater than 0."
        ]);
    }

    [Fact]
    public void BulkBatchSizeFor_IndexValueWinsAndAnUnsetIndexTakesTheSharedOne()
    {
        var options = new MeilisearchOptions
        {
            SharedIndexSettings = new() { BulkBatchSize = 500 },
            Indexes = new Dictionary<string, MeilisearchIndexSettings>
            {
                ["people"] = new() { BulkBatchSize = 50 },
                ["places"] = new(),
            },
        };

        options.BulkBatchSizeFor("PEOPLE").ShouldBe(50);
        options.BulkBatchSizeFor("places").ShouldBe(500);
        options.BulkBatchSizeFor("other").ShouldBe(500);
        new MeilisearchOptions().BulkBatchSizeFor("other").ShouldBeNull();
    }

    [Fact]
    public async Task SearchAsync_MaxTotalHitsNotPositiveOnAMonitorThatDoesNotValidate_ReturnsFailedResultNamingTheSetting()
    {
        var options = new MeilisearchOptions
        {
            SharedIndexSettings = new MeilisearchIndexSettings { ConnectionString = "http://localhost:7700" },
            MaxTotalHits = 0,
        };
        var monitor = new TestOptionsMonitor<MeilisearchOptions>(options);
        var provider = new MeilisearchSearchProviderAsync(null!,
            new MeilisearchClientFactory(new MeilisearchClientSettingsManager(monitor)), monitor);

        var result = await provider.SearchAsync(new SearchQuery { Index = "people" }, TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().Message.ShouldBe($"{Key}:MaxTotalHits is 0, but it must be greater than 0.");
    }

    [Fact]
    public async Task Providers_ReloadMakesBulkBatchSizeInvalid_ReturnFailedResultsNamingTheSetting()
    {
        // Nothing listens on port 1, so a call that got past the options would fail with a connection error instead.
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{Key}:SharedIndexSettings:ConnectionString"] = "http://127.0.0.1:1",
        }).Build();
        using var services = new ServiceCollection()
            .AddLogging()
            .AddBetterSearch(configuration, x => x.AddMeilisearch())
            .BuildServiceProvider();
        services.GetRequiredService<IStartupValidator>().Validate();
        configuration[$"{Key}:SharedIndexSettings:BulkBatchSize"] = "0";
        // The options monitor reads the reloaded options in the reload's change callback, so the validation failure
        // surfaces there too; a file reload runs that callback on its own thread, away from the callers.
        Should.Throw<AggregateException>(configuration.Reload).InnerException.ShouldBeOfType<OptionsValidationException>();
        using var scope = services.CreateScope();
        var indexing = scope.ServiceProvider.GetRequiredService<IIndexingProviderAsync>();
        var search = scope.ServiceProvider.GetRequiredService<ISearchProviderAsync>();
        var monitor = services.GetRequiredService<IOptionsMonitor<MeilisearchOptions>>();
        var token = TestContext.Current.CancellationToken;
        var document = new SearchDocument { Id = "1", Type = "TestDocument", Index = "people" };
        const string setting = $"{Key}:SharedIndexSettings:BulkBatchSize is 0, but it must be greater than 0.";

        Should.Throw<OptionsValidationException>(() => monitor.CurrentValue);
        var many = await indexing.IndexMultipleDocumentsAsync([document], token);
        var one = await indexing.IndexDocumentAsync(document, token);
        var found = await search.SearchAsync(new SearchQuery { Index = "people" }, token);
        var phrasePrefix = new PrefixPhraseSubQueryTranslator(monitor);
        var spatial = new SpatialSearchSubQueryTranslator(monitor);
        var lucene = new LuceneSubQueryTranslator(monitor);
        var translated = new[]
        {
            await phrasePrefix.TranslateAsync(new PrefixPhraseSubQuery { Field = "Name", Value = "jane d" }, [phrasePrefix]),
            await spatial.TranslateAsync(new SpatialSearchQuery { Field = "Location", Distance = 10 }, [spatial]),
            await lucene.TranslateAsync(new LuceneSubQuery { LuceneQuery = "Name:\"a b\"" }, [lucene]),
        };

        many.IsFailed.ShouldBeTrue();
        many.Errors.Single().Message.ShouldContain(setting);
        one.IsFailed.ShouldBeTrue();
        one.Errors.Single().Message.ShouldContain(setting);
        found.IsFailed.ShouldBeTrue();
        found.Errors.Single().Message.ShouldContain(setting);
        foreach (var result in translated)
        {
            result.IsFailed.ShouldBeTrue();
            result.Errors.Single().Message.ShouldContain(setting);
        }
    }
}
