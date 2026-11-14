using Bielu.BetterSearch.Abstractions.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bielu.BetterSearch.HealthChecks;

/// <summary>Reports the engine behind a search provider as healthy when <see cref="ISearchProviderAsync.PingAsync"/> succeeds.</summary>
internal sealed class BetterSearchHealthCheck(ISearchProviderAsync provider, string? index) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await provider.PingAsync(index, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                return HealthCheckResult.Healthy($"{provider.GetType().Name} reached its engine.");
            }

            var errors = string.Join(" ", result.Errors.Select(error => error.Message));
            return new HealthCheckResult(context.Registration.FailureStatus, $"{provider.GetType().Name} can't reach its engine: {errors}");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, $"{provider.GetType().Name} can't reach its engine: {e.Message}", e);
        }
    }
}
