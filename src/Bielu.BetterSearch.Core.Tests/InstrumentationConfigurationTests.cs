using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Bielu.BetterSearch.Core.Tests;

public class InstrumentationConfigurationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string NewIndex() => $"index-{Guid.NewGuid():N}";

    private static ServiceProvider Build(Dictionary<string, string?> settings, Action<Bielu.BetterSearch.Configuration.Configurator> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBetterSearch(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), configure);
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("true", "count rejected")]
    public async Task AddBetterSearch_RecordErrorMessagesSetting_DecidesWhetherSpanHoldsMessage(string? setting, string? expectedDescription)
    {
        var settings = new Dictionary<string, string?>();
        if (setting is not null)
        {
            settings[$"{BetterSearchInstrumentationOptions.SectionKey}:RecordErrorMessages"] = setting;
        }

        await using var provider = Build(settings, c => c.AddProvider<FailingCountProvider, FakeIndexingProvider>("fake"));
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<ISearchServiceAsync>().CountAsync(new SearchQuery { Index = index }, Token);

        capture.SingleActivity().StatusDescription.ShouldBe(expectedDescription);
    }

    [Fact]
    public async Task AddBetterSearch_RecordErrorMessagesNotABoolean_FailsAtStartupNamingTheSetting()
    {
        var key = $"{BetterSearchInstrumentationOptions.SectionKey}:RecordErrorMessages";
        await using var provider = Build(new Dictionary<string, string?> { [key] = "maybe" },
            c => c.AddProvider<FakeSearchProvider, FakeIndexingProvider>("fake"));

        var exception = Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());

        exception.Message.ShouldContain(key);
    }

    [Fact]
    public async Task SearchAsync_RoutedIndex_RecordsOneSpanFromTheRoutedProvider()
    {
        var index = NewIndex();
        await using var provider = Build([], c => c
            .AddProvider<FakeSearchProvider, FakeIndexingProvider>("first")
            .AddProvider<OtherSearchProvider, OtherIndexingProvider>("second")
            .RouteIndex(index, "second"));
        using var capture = new TelemetryCapture(index);
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<ISearchServiceAsync>().SearchAsync(new SearchQuery { Index = index }, Token);

        result.Value.Items.Count().ShouldBe(2);
        var activity = capture.SingleActivity();
        activity.OperationName.ShouldBe($"search {index}");
        activity.GetTagItem(BetterSearchTelemetry.ProviderTag).ShouldBe(nameof(OtherSearchProvider));
        activity.GetTagItem(BetterSearchTelemetry.HitCountTag).ShouldBe(2);
        capture.Measurements("bettersearch.operations").ShouldHaveSingleItem();
    }

    /// <summary>A provider whose count fails, so a test can read the span's status description.</summary>
    internal sealed class FailingCountProvider : ISearchProviderAsync
    {
        public Task<FluentResults.Result<Abstractions.Models.ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(FluentResults.Result.Ok<Abstractions.Models.ISearchResult>(FakeSearchProvider.Results(0, 0)));

        public Task<FluentResults.Result<long>> CountAsync(ISearchQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(FluentResults.Result.Fail<long>("count rejected"));
    }
}
