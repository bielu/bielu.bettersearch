using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Abstractions.Services.NullImplementations;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.HealthChecks;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;

namespace Bielu.BetterSearch.Core.Tests;

public class HealthCheckTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<HealthReport> CheckAsync(Action<IServiceCollection> register, Action<IHealthChecksBuilder> addCheck)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        register(services);
        addCheck(services.AddHealthChecks());
        await using var provider = services.BuildServiceProvider();
        return await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync(Token);
    }

    [Fact]
    public async Task AddBetterSearchHealthCheck_PingSucceeds_ReportsHealthy()
    {
        var fake = new FakeSearchProvider();
        var report = await CheckAsync(s => s.AddSingleton<ISearchProviderAsync>(fake), b => b.AddBetterSearchHealthCheck());

        var entry = report.Entries[BetterSearchHealthChecksBuilderExtensions.DefaultName];
        entry.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task AddBetterSearchHealthCheck_PingFails_ReportsFailureStatusWithEveryError()
    {
        var fake = new FakeSearchProvider { Ping = (_, _) => Task.FromResult(Result.Fail("connection refused").WithError("retry later")) };
        var report = await CheckAsync(s => s.AddSingleton<ISearchProviderAsync>(fake),
            b => b.AddBetterSearchHealthCheck("search", failureStatus: HealthStatus.Degraded));

        var entry = report.Entries["search"];
        entry.Status.ShouldBe(HealthStatus.Degraded);
        entry.Description.ShouldBe("FakeSearchProvider can't reach its engine: connection refused retry later");
    }

    [Fact]
    public async Task AddBetterSearchHealthCheck_PingThrows_ReportsUnhealthyWithException()
    {
        var fake = new FakeSearchProvider { Ping = (_, _) => throw new InvalidOperationException("socket closed") };
        var report = await CheckAsync(s => s.AddSingleton<ISearchProviderAsync>(fake), b => b.AddBetterSearchHealthCheck());

        var entry = report.Entries[BetterSearchHealthChecksBuilderExtensions.DefaultName];
        entry.Status.ShouldBe(HealthStatus.Unhealthy);
        entry.Description!.ShouldContain("socket closed");
        entry.Exception.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task AddBetterSearchHealthCheck_ProviderKeyAndIndex_PingsKeyedProviderWithIndex()
    {
        string? pingedIndex = null;
        var keyed = new FakeSearchProvider { Ping = (index, _) => { pingedIndex = index; return Task.FromResult(Result.Ok()); } };
        var unkeyed = new FakeSearchProvider { Ping = (_, _) => Task.FromResult(Result.Fail("wrong provider")) };
        var report = await CheckAsync(
            s =>
            {
                s.AddSingleton<ISearchProviderAsync>(unkeyed);
                s.AddKeyedSingleton<ISearchProviderAsync>("secondary", keyed);
            },
            b => b.AddBetterSearchHealthCheck(providerKey: "secondary", index: "products"));

        report.Status.ShouldBe(HealthStatus.Healthy);
        pingedIndex.ShouldBe("products");
    }

    [Fact]
    public async Task AddBetterSearchHealthCheck_TwoRoutedProvidersNoDefaultNoKey_ReportsFailureNamingTheKeys()
    {
        var report = await CheckAsync(
            s => s.AddBetterSearch(c => c
                .AddProvider<FakeSearchProvider, FakeIndexingProvider>("first")
                .AddProvider<OtherSearchProvider, OtherIndexingProvider>("second")
                .RouteIndex("products", "second")),
            b => b.AddBetterSearchHealthCheck(failureStatus: HealthStatus.Degraded));

        var entry = report.Entries[BetterSearchHealthChecksBuilderExtensions.DefaultName];
        entry.Status.ShouldBe(HealthStatus.Degraded);
        entry.Exception.ShouldBeNull();
        entry.Description.ShouldBe(
            "More than one search provider is registered ('first', 'second') and none is the default, so AddBetterSearchHealthCheck can't choose one. " +
            "Pass providerKey, for example AddBetterSearchHealthCheck(providerKey: \"first\"), once for each provider to check.");
    }

    [Fact]
    public async Task AddBetterSearchHealthCheck_TwoProvidersWithDefaultNoKey_PingsTheDefault()
    {
        var report = await CheckAsync(
            s => s.AddBetterSearch(c => c
                .AddProvider<FakeSearchProvider, FakeIndexingProvider>("first")
                .AddProvider<OtherSearchProvider, OtherIndexingProvider>("second")
                .SetDefaultProvider("first")),
            b => b.AddBetterSearchHealthCheck());

        report.Entries[BetterSearchHealthChecksBuilderExtensions.DefaultName].Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task PingAsync_ProviderDoesNotOverride_FailsSayingItCantBeChecked()
    {
        ISearchProviderAsync provider = new SearchOnlyProvider();

        var result = await provider.PingAsync(cancellationToken: Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("SearchOnlyProvider does not implement PingAsync, so the engine behind it can't be checked.");
    }

    [Fact]
    public async Task PingAsync_DummyProvider_FailsSayingNoProviderIsRegistered()
    {
        ISearchProviderAsync provider = new DummySearchProviderAsync();

        var result = await provider.PingAsync(cancellationToken: Token);

        result.Errors.ShouldHaveSingleItem().Message.ShouldStartWith("No search provider is registered.");
    }
}
