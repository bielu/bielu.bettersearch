using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Hosting;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

/// <summary>
/// Starts the AppHost's Elasticsearch with security on, the first time a test asks for it, so test runs that don't
/// need it don't start it. It serves HTTPS with a certificate signed by the CA it generated, and the elastic user's
/// password is set here; an API key and a service account token are created once it is up.
/// </summary>
public sealed class SecuredElasticsearchResource : IHostedService
{
    private const string ResourceName = "elasticsearch-secured";
    private const string PasswordParameter = "elasticsearch-secured-password";

    private readonly SemaphoreSlim _starting = new(1, 1);
    private DistributedApplication? _app;

    public string Password { get; } = $"Pw{Guid.NewGuid():N}";

    /// <summary>The HTTPS endpoint, once started.</summary>
    public Uri Endpoint { get; private set; } = null!;

    /// <summary>The SHA-256 fingerprint of the CA the server generated, as it presents it in its chain.</summary>
    public string CaFingerprint { get; private set; } = null!;

    /// <summary>The <c>encoded</c> value of an API key created with the elastic user.</summary>
    public string ApiKey { get; private set; } = null!;

    /// <summary>
    /// A token of the <c>elastic/fleet-server</c> service account, which may create, write and read indexes named
    /// <c>content-*</c>. Elasticsearch's token API needs a paid license, so a service account gives the bearer token.
    /// </summary>
    public string ServiceAccountToken { get; private set; } = null!;


    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        await _starting.WaitAsync(cancellationToken);
        try
        {
            if (ServiceAccountToken is not null)
            {
                return;
            }

            if (_app is not null)
            {
                // An earlier start failed part way; start again from scratch.
                await _app.DisposeAsync();
                _app = null;
            }

            // A first run may need to pull the image. A cancelled test stops the start too.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));
            var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Bielu_BetterSearch_Examples_AppHost>(timeout.Token);
            builder.Configuration[$"Parameters:{PasswordParameter}"] = Password;
            foreach (var other in builder.Resources.Where(r => r.Name is not (ResourceName or PasswordParameter)).ToList())
            {
                builder.Resources.Remove(other);
            }

            _app = await builder.BuildAsync(timeout.Token);
            await _app.StartAsync(timeout.Token);
            await _app.ResourceNotifications.WaitForResourceHealthyAsync(ResourceName, timeout.Token);
            Endpoint = _app.GetEndpoint(ResourceName, "https");

            using var client = CreateAdminClient(fingerprint => CaFingerprint = fingerprint);
            ApiKey = (await PostAsync(client, "/_security/api_key", new { name = "bettersearch-tests" }, timeout.Token))
                .GetProperty("encoded").GetString()!;
            ServiceAccountToken = (await PostAsync(client, $"/_security/service/elastic/fleet-server/credential/token/bettersearch-{Guid.NewGuid():N}", null, timeout.Token))
                .GetProperty("token").GetProperty("value").GetString()!;
        }
        finally
        {
            _starting.Release();
        }
    }

    /// <summary>Deletes <paramref name="index"/> as the elastic user, which every scheme's tests can clean up with.</summary>
    public async Task DeleteIndexAsync(string index)
    {
        using var client = CreateAdminClient(_ => { });
        using var response = await client.DeleteAsync(new Uri(Endpoint, index), CancellationToken.None);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    /// <summary>A client logged in as the elastic user that trusts the server's CA and reports its fingerprint.</summary>
    private HttpClient CreateAdminClient(Action<string> caFingerprint)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, chain, _) =>
            {
                // The server sends its certificate and then the CA's.
                caFingerprint(chain!.ChainElements[^1].Certificate.GetCertHashString(HashAlgorithmName.SHA256));
                return true;
            },
        };
        var client = new HttpClient(handler) { BaseAddress = Endpoint };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"elastic:{Password}")));
        return client;
    }

    private static async Task<JsonElement> PostAsync(HttpClient client, string path, object? body, CancellationToken cancellationToken)
    {
        using var response = body is null
            ? await client.PostAsync(path, null, cancellationToken)
            : await client.PostAsJsonAsync(path, body, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"POST {path} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }

        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }
}
