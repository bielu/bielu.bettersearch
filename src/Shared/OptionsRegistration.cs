using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.Services;

internal static class OptionsRegistration
{
    /// <summary>
    /// Binds <typeparamref name="TOptions"/> to the <paramref name="sectionKey"/> section of
    /// <paramref name="configuration"/> and validates them at startup with <typeparamref name="TValidator"/>.
    /// AddBetterSearch can run more than once on a collection: each distinct configuration is bound once, in call
    /// order, since binding one configuration twice would add every list entry twice, and the validator runs once.
    /// </summary>
    public static void BindAndValidateOnStart<TOptions, TValidator>(IServiceCollection services, IConfiguration configuration, string sectionKey)
        where TOptions : class
        where TValidator : class, IValidateOptions<TOptions>
    {
        BindOnStart<TOptions>(services, configuration, sectionKey);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<TOptions>, TValidator>());
    }

    /// <summary>
    /// Binds <typeparamref name="TOptions"/> as <see cref="BindAndValidateOnStart{TOptions,TValidator}"/> does, for options
    /// with no rule beyond what the binder checks, and reads them at startup, so a value the binder can't convert fails
    /// there, naming its key.
    /// </summary>
    public static void BindOnStart<TOptions>(IServiceCollection services, IConfiguration configuration, string sectionKey)
        where TOptions : class
    {
        var builder = services.AddOptions<TOptions>();
        if (!services.Any(service => !service.IsKeyedService &&
                                     service.ImplementationInstance is BoundConfiguration<TOptions> bound &&
                                     ReferenceEquals(bound.Configuration, configuration)))
        {
            services.AddSingleton(new BoundConfiguration<TOptions>(configuration));
            builder.Bind(configuration.GetSection(sectionKey));
        }

        builder.ValidateOnStart();
    }

    /// <summary>Marks a configuration <typeparamref name="TOptions"/> are already bound to.</summary>
    private sealed record BoundConfiguration<TOptions>(IConfiguration Configuration);
}
