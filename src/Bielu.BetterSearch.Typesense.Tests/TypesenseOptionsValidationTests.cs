using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.Tests;
using Bielu.BetterSearch.Typesense.Configuration;
using Bielu.BetterSearch.Typesense.DepedencyInjection;
using FluentResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Bielu.BetterSearch.Typesense.Tests;

public class TypesenseOptionsValidationTests : OptionsValidationTestBase
{
    private const string Key = "Bielu:BetterSearch:Typesense";

    protected override string OptionsKey => Key;

    protected override void AddProvider(Configurator search, string key) => search.AddTypesense(key);

    private IReadOnlyList<string>? Validate(string setting, string value) => ValidateOnStart(new Dictionary<string, string?>
    {
        ["SharedIndexSettings:ConnectionString"] = "http://localhost:8108",
        [setting] = value,
    })?.Failures.ToList();

    [Fact]
    public void ValidateOnStart_SchemaCheckIntervalNegative_FailsNamingTheSetting()
    {
        Validate("SchemaCheckInterval", "-00:00:01").ShouldBe([$"{Key}:SchemaCheckInterval is -00:00:01, but it must not be negative."]);
    }

    [Fact]
    public void ValidateOnStart_SchemaCheckIntervalZero_Passes()
    {
        Validate("SchemaCheckInterval", "00:00:00").ShouldBeNull();
    }
    [Fact]
    public async Task Providers_ReloadMakesSchemaCheckIntervalInvalid_ReturnFailedResultsNamingTheSetting()
    {
        // Nothing listens on port 1, so a call that got past the options would fail with a connection error instead.
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{Key}:SharedIndexSettings:ConnectionString"] = "http://127.0.0.1:1",
        }).Build();
        using var services = new ServiceCollection()
            .AddLogging()
            .AddBetterSearch(configuration, x => x.AddTypesense())
            .BuildServiceProvider();
        services.GetRequiredService<IStartupValidator>().Validate();
        configuration[$"{Key}:SchemaCheckInterval"] = "-00:00:01";
        // The options monitor reads the reloaded options in the reload's change callback, so the validation failure
        // surfaces there too; a file reload runs that callback on its own thread, away from the callers.
        Should.Throw<AggregateException>(configuration.Reload).InnerException.ShouldBeOfType<OptionsValidationException>();
        using var scope = services.CreateScope();
        var indexing = scope.ServiceProvider.GetRequiredService<IIndexingProviderAsync>();
        var search = scope.ServiceProvider.GetRequiredService<ISearchProviderAsync>();
        var token = TestContext.Current.CancellationToken;
        var document = new SearchDocument { Id = "1", Type = "TestDocument", Index = "people" };
        const string setting = $"{Key}:SchemaCheckInterval is -00:00:01, but it must not be negative.";

        Should.Throw<OptionsValidationException>(() => services.GetRequiredService<IOptionsMonitor<TypesenseOptions>>().CurrentValue);
        var results = new ResultBase[]
        {
            await indexing.IndexMultipleDocumentsAsync([document], token),
            await indexing.IndexDocumentAsync(document, token),
            await indexing.RemoveDocumentsAsync("people", ["1"], token),
            await search.SearchAsync(new SearchQuery { Index = "people" }, token),
        };

        foreach (var result in results)
        {
            result.IsFailed.ShouldBeTrue();
            result.Errors.Single().Message.ShouldContain(setting);
        }
    }
}
