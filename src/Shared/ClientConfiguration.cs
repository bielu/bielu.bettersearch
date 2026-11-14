using Microsoft.Extensions.DependencyInjection;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// A callback a provider's <c>Add</c> method was given to configure the provider's client, registered once for each call
/// that passed one. The client factory runs every registered callback, in the order they were registered, after it has
/// applied the options, so a callback can override them. It runs once for each client the factory creates and caches,
/// not once per request.
/// </summary>
internal sealed class ClientConfiguration<TSettings>(Action<TSettings> configure)
{
    private Action<TSettings> Configure { get; } = configure;

    /// <summary>Registers <paramref name="configure"/>, when it is not <c>null</c>.</summary>
    public static void Register(IServiceCollection services, Action<TSettings>? configure)
    {
        if (configure != null)
        {
            services.AddSingleton(new ClientConfiguration<TSettings>(configure));
        }
    }

    /// <summary>Runs each of <paramref name="configurations"/> on <paramref name="settings"/>, in order.</summary>
    public static void Apply(IEnumerable<ClientConfiguration<TSettings>> configurations, TSettings settings)
    {
        foreach (var configuration in configurations)
        {
            configuration.Configure(settings);
        }
    }
}
