using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
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
    /// <param name="providerKey">
    /// The key the provider was added under in <c>AddBetterSearch</c>; <c>null</c> checks the default provider. With two
    /// or more providers and none set as the default, a check without a key reports <paramref name="failureStatus"/>,
    /// naming the registered keys to pass.
    /// </param>
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
            sp => providerKey is null
                ? DefaultProviderCheck(sp, index)
                : new BetterSearchHealthCheck(sp.GetRequiredKeyedService<ISearchProviderAsync>(providerKey), index),
            failureStatus,
            tags,
            timeout));
    }

    /// <summary>
    /// The check of the unkeyed provider, or one that fails naming the keys when there are several providers and none is
    /// the default, as the unkeyed provider can't be resolved then.
    /// </summary>
    private static IHealthCheck DefaultProviderCheck(IServiceProvider sp, string? index)
    {
        if (sp.GetService<SearchApplicationConfiguration>() is { DefaultProviderKey: null, ProviderKeys: { Count: > 1 } keys })
        {
            return new FailingHealthCheck(
                $"More than one search provider is registered ({string.Join(", ", keys.Select(key => $"'{key}'"))}) and none is the default, " +
                $"so AddBetterSearchHealthCheck can't choose one. Pass providerKey, for example AddBetterSearchHealthCheck(providerKey: \"{keys[0]}\"), " +
                "once for each provider to check.");
        }

        return new BetterSearchHealthCheck(sp.GetRequiredService<ISearchProviderAsync>(), index);
    }
}
