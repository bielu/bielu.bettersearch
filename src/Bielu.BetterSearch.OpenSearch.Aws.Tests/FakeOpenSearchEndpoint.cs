using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Bielu.BetterSearch.OpenSearch.Aws.Tests;

/// <summary>A local HTTP endpoint that answers every request with an empty JSON object and keeps what it received.</summary>
internal sealed class FakeOpenSearchEndpoint : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Task _serving;

    public FakeOpenSearchEndpoint()
    {
        Url = $"http://127.0.0.1:{FreePort()}/";
        _listener.Prefixes.Add(Url);
        _listener.Start();
        _serving = Task.Run(ServeAsync);
    }

    public string Url { get; }

    public List<ReceivedRequest> Requests { get; } = [];

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            using var body = new MemoryStream();
            await context.Request.InputStream.CopyToAsync(body);
            var headers = context.Request.Headers.AllKeys.OfType<string>()
                .ToDictionary(name => name, name => context.Request.Headers[name]!, StringComparer.OrdinalIgnoreCase);
            lock (Requests)
            {
                Requests.Add(new ReceivedRequest(context.Request.HttpMethod, context.Request.Url!.AbsolutePath,
                    context.Request.Url.Query.TrimStart('?'), headers, body.ToArray()));
            }

            var response = Encoding.UTF8.GetBytes("{}");
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(response);
            context.Response.Close();
        }
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    public void Dispose()
    {
        _listener.Close();
        _serving.Wait(TimeSpan.FromSeconds(5));
    }
}

internal sealed record ReceivedRequest(string Method, string Path, string Query, IReadOnlyDictionary<string, string> Headers, byte[] Body);
