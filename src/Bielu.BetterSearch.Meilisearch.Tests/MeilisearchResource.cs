using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Hosting;

namespace Bielu.BetterSearch.Meilisearch.Tests;

/// <summary>
/// Starts Meilisearch through the examples AppHost, once for the whole test run. Only the search
/// engine runs as a container; the code under test runs in the test process, so coverage is collected
/// for it as usual.
/// </summary>
public sealed class MeilisearchResource : IHostedService
{
    private const string ResourceName = "meilisearch";

    private DistributedApplication? _app;

    /// <summary>The HTTP endpoint of the running server, once started.</summary>
    public Uri? Endpoint { get; private set; }

    /// <summary>The master key the AppHost generated for the server, once started.</summary>
    public string? MasterKey { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // A first run may need to pull the image.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Bielu_BetterSearch_Examples_AppHost>(timeout.Token);
        // The AppHost runs every engine and UI; these tests only need Meilisearch and its key.
        foreach (var other in builder.Resources.Where(r => r.Name != ResourceName && r is not ParameterResource).ToList())
        {
            builder.Resources.Remove(other);
        }

        _app = await builder.BuildAsync(timeout.Token);
        await _app.StartAsync(timeout.Token);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync(ResourceName, timeout.Token);

        Endpoint = _app.GetEndpoint(ResourceName, "http");
        var meilisearch = builder.Resources.OfType<Aspire.Hosting.ApplicationModel.MeilisearchResource>().Single();
        MasterKey = await meilisearch.MasterKeyParameter.GetValueAsync(timeout.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
