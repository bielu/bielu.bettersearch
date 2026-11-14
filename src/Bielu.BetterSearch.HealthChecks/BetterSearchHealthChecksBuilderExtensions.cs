using Bielu.BetterSearch.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bielu.BetterSearch.HealthChecks;

public static class BetterSearchHealthChecksBuilderExtensions
{
    /// <summary>The name <see cref="AddBetterSearchHealthCheck"/> registers the check under when none is given.</summary>
    public const string DefaultName = "bettersearch";

    /// <summary>
    /// Adds a health check that pings the engine through <see cref="ISearchProviderAsync.PingAsync"/>: healthy when it
    /// answers, <paramref name="failureStatus"/> (by default <see cref="HealthStatus.Unhealthy"/>) when it doesn't.
    /// </summary>
    /// <param name="builder">The builder <c>AddHealthChecks</c> returned.</param>
    /// <param name="name">The name the check reports under.</param>
    /// <param name="providerKey">The key the provider was added under in <c>AddBetterSearch</c>; <c>null</c> checks the default provider.</param>
    /// <param name="index">The index whose connection settings are used; <c>null</c> uses the shared settings.</param>
    /// <param name="failureStatus">The status reported when the ping fails; <c>null</c> reports <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Tags for filtering the check, for example in <c>MapHealthChecks</c>.</param>
    /// <param name="timeout">How long the check may take; <c>null</c> uses the health check service's default.</param>
    public static IHealthChecksBuilder AddBetterSearchHealthCheck(
        this IHealthChecksBuilder builder,
        string name = DefaultName,
        string? providerKey = null,
        string? index = null,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new BetterSearchHealthCheck(
                providerKey is null
                    ? sp.GetRequiredService<ISearchProviderAsync>()
                    : sp.GetRequiredKeyedService<ISearchProviderAsync>(providerKey),
                index),
            failureStatus,
            tags,
            timeout));
    }
}
