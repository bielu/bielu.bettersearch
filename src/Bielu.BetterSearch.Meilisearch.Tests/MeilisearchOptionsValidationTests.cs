using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Meilisearch.DepedencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Bielu.BetterSearch.Meilisearch.Tests;

public class MeilisearchOptionsValidationTests
{
    private const string Key = "Bielu:BetterSearch:Meilisearch";

    private static ServiceProvider Services(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
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
}
