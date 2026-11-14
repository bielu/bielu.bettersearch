using Bielu.BetterSearch.Abstractions.Services;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

/// <summary><see cref="ISearchProviderAsync.PingAsync"/> succeeds against the engine the test run started.</summary>
public abstract class PingTestBase(ISearchProviderAsync searchProvider)
{
    [Fact]
    public async Task PingAsync_EngineRunning_Succeeds()
    {
        var result = await searchProvider.PingAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public async Task PingAsync_Cancelled_Throws()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => searchProvider.PingAsync(cancellationToken: cancelled.Token));
    }
}
