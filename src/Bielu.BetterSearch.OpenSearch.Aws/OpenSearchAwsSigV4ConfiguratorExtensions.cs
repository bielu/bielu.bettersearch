using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.DepedencyInjection;
using Bielu.BetterSearch.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.OpenSearch.Aws;

public static class OpenSearchAwsSigV4ConfiguratorExtensions
{
    /// <summary>
    /// Signs every request the OpenSearch provider sends with AWS SigV4, for Amazon OpenSearch Service with IAM
    /// authentication or Amazon OpenSearch Serverless. The settings are read from
    /// <c>Bielu:BetterSearch:OpenSearch:AwsSigV4</c> and then <paramref name="configure"/>, and validated at startup.
    /// Leave the OpenSearch <c>AuthenticationType</c> at <c>NONE</c>: the signature is the request's
    /// <c>Authorization</c> header. Each cached OpenSearch client gets its own signing connection, with the options
    /// current when the client is created.
    /// </summary>
    public static Configurator AddOpenSearchAwsSigV4(this Configurator configurator,
        Action<OpenSearchAwsSigV4Options>? configure = null)
    {
        OptionsRegistration.BindAndValidateOnStart<OpenSearchAwsSigV4Options, OpenSearchAwsSigV4OptionsValidator>(
            configurator.Services, configurator.AppConfiguration, OpenSearchAwsSigV4Options.AwsSigV4OptionsKey);
        if (configure != null)
        {
            configurator.Services.Configure(configure);
        }

        return configurator.UseOpenSearchConnection(services =>
            AwsSigV4Connections.Create(services.GetRequiredService<IOptionsMonitor<OpenSearchAwsSigV4Options>>().CurrentValue));
    }
}
