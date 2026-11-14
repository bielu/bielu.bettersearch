using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Hosting;

namespace Bielu.BetterSearch.Typesense.Tests;

/// <summary>
/// Starts Typesense through the examples AppHost, once for the whole test run. Only the search
/// engine runs as a container; the code under test runs in the test process, so coverage is collected
/// for it as usual.
/// </summary>
public sealed class TypesenseResource : IHostedService
{
    private const string ResourceName = "typesense";
    private const string ApiKeyName = "typesense-api-key";

    private readonly SemaphoreSlim _starting = new(1, 1);
    private DistributedApplication? _app;

    /// <summary>The HTTP endpoint of the running server, once started.</summary>
    public Uri? Endpoint { get; private set; }

    /// <summary>The admin key the AppHost generated for the server, once started.</summary>
    public string? ApiKey { get; private set; }

    /// <summary>
    /// The endpoint, starting the server first when the host has not yet. Startup validation reads the options
    /// before the host starts its hosted services, so the options ask for the endpoint through this.
    /// </summary>
    public Uri EnsureStarted()
    {
        StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        return Endpoint!;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _starting.WaitAsync(cancellationToken);
        try
        {
            if (Endpoint is not null)
            {
                return;
            }

            // A first run may need to pull the image.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));

            var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Bielu_BetterSearch_Examples_AppHost>(timeout.Token);
            // The AppHost runs every engine and UI; these tests only need Typesense and its key.
            foreach (var other in builder.Resources.Where(r => r.Name != ResourceName && r is not ParameterResource).ToList())
            {
                builder.Resources.Remove(other);
            }

            _app = await builder.BuildAsync(timeout.Token);
            await _app.StartAsync(timeout.Token);
            await _app.ResourceNotifications.WaitForResourceHealthyAsync(ResourceName, timeout.Token);

            // The key is read before the endpoint is set, as a set endpoint means the server is ready to use.
            var apiKey = builder.Resources.OfType<ParameterResource>().Single(parameter => parameter.Name == ApiKeyName);
            ApiKey = await apiKey.GetValueAsync(timeout.Token);
            Endpoint = _app.GetEndpoint(ResourceName, "http");
        }
        finally
        {
            _starting.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
