using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Meilisearch.DepedencyInjection;
using Bielu.BetterSearch.Meilisearch.Services.Queries;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Bielu.BetterSearch.Meilisearch.Tests;

public class MeilisearchOptionsValidationTests
{
    private const string Key = "Bielu:BetterSearch:Meilisearch";

    private static ServiceProvider Services(Dictionary<string, string?> settings) =>
        ServicesFor(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

    private static ServiceProvider ServicesFor(IConfiguration configuration)
    {
        return new ServiceCollection()
            .AddLogging()
            .AddBetterSearch(configuration, x => x.AddMeilisearch())
            .BuildServiceProvider();
    }

    /// <summary>Runs the checks a host runs when it starts.</summary>
    private static void ValidateOnStart(ServiceProvider services) =>
        services.GetRequiredService<IStartupValidator>().Validate();

    [Fact]
    public void ValidateOnStart_PositiveBulkBatchSizes_Passes()
    {
        using var services = Services(new()
        {
            [$"{Key}:SharedIndexSettings:ConnectionString"] = "http://localhost:7700",
            [$"{Key}:SharedIndexSettings:BulkBatchSize"] = "500",
            [$"{Key}:Indexes:people:BulkBatchSize"] = "1",
        });

        Should.NotThrow(() => ValidateOnStart(services));
        services.GetRequiredService<IOptions<MeilisearchOptions>>().Value.BulkBatchSizeFor("People").ShouldBe(1);
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
    public async Task Providers_ReloadMakesBulkBatchSizeInvalid_ReturnFailedResultsNamingTheSetting()
    {
        // Nothing listens on port 1, so a call that got past the options would fail with a connection error instead.
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{Key}:SharedIndexSettings:ConnectionString"] = "http://127.0.0.1:1",
        }).Build();
        using var services = ServicesFor(configuration);
        ValidateOnStart(services);
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
