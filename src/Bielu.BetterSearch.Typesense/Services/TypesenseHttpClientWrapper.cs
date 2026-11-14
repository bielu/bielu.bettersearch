using System.Net;
using System.Text.Json;

namespace Bielu.BetterSearch.Typesense.Services;

/// <summary>An answer from Typesense: its status code and body.</summary>
public sealed record TypesenseResponse(HttpStatusCode StatusCode, string Body)
{
    public bool IsSuccess => (int)StatusCode is >= 200 and < 300;

    /// <summary>The <c>message</c> Typesense puts in an error body, or the body itself.</summary>
    public string Message
    {
        get
        {
            try
            {
                using var json = JsonDocument.Parse(Body);
                return json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("message", out var message)
                    ? message.GetString() ?? Body
                    : Body;
            }
            catch (JsonException)
            {
                return Body;
            }
        }
    }

    public string Describe() => $"Typesense answered {(int)StatusCode}: {Message}";
}

/// <summary>
/// A thin client, resolved through <c>IClientFactoryAsync&lt;TypesenseHttpClientWrapper&gt;</c>, for the parts of the Typesense HTTP API the provider uses. A request that cannot reach a node, or
/// gets a 5xx answer, is sent to the next node; the last node's answer or exception is the result. A <c>DELETE</c> moves
/// on only when it could not connect: a node that timed out or answered 5xx may have applied it, and a replay on another
/// node would report only what was left to delete.
/// </summary>
public sealed class TypesenseHttpClientWrapper
{
    private const string ApiKeyHeader = "X-TYPESENSE-API-KEY";

    private readonly HttpClient httpClient;
    private readonly IReadOnlyList<Uri> nodes;
    private readonly string? apiKey;

    // Not a primary constructor: those are public, and only the client factory creates clients.
#pragma warning disable BIELU002
    internal TypesenseHttpClientWrapper(HttpClient httpClient, IReadOnlyList<Uri> nodes, string? apiKey)
    {
        this.httpClient = httpClient;
        this.nodes = nodes;
        this.apiKey = apiKey;
    }
#pragma warning restore BIELU002

    public IReadOnlyList<Uri> Nodes => nodes;

    public async Task<TypesenseResponse> SendAsync(HttpMethod method, string pathAndQuery, Func<HttpContent>? content,
        CancellationToken cancellationToken)
    {
        var replayable = method != HttpMethod.Delete;
        for (var i = 0; ; i++)
        {
            var last = i == nodes.Count - 1;
            using var request = new HttpRequestMessage(method, Resolve(nodes[i], pathAndQuery));
            if (apiKey != null)
            {
                request.Headers.Add(ApiKeyHeader, apiKey);
            }

            request.Content = content?.Invoke();
            try
            {
                using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if ((int)response.StatusCode < 500 || last || !replayable)
                {
                    return new TypesenseResponse(response.StatusCode, body);
                }
            }
            catch (Exception e) when (!last && !cancellationToken.IsCancellationRequested
                                              && (replayable ? e is HttpRequestException or TaskCanceledException : NeverSent(e)))
            {
                // The node is unreachable or timed out: try the next one.
            }
        }
    }

    /// <summary>Whether the request failed before a connection to the node was made, so the node never saw it.</summary>
    private static bool NeverSent(Exception e) => e is HttpRequestException
    {
        HttpRequestError: HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError or HttpRequestError.SecureConnectionError
    };

    /// <summary>
    /// The node's URL for <paramref name="pathAndQuery"/>. A <c>.</c> or <c>..</c> segment, even escaped, would be
    /// resolved away and send the request to another resource, such as the collection rather than one of its documents,
    /// so it throws instead.
    /// </summary>
    internal static Uri Resolve(Uri node, string pathAndQuery)
    {
        var path = pathAndQuery.Split('?', 2)[0];
        if (path.Split('/').Any(segment => Uri.UnescapeDataString(segment) is "." or ".."))
        {
            throw new ArgumentException($"The Typesense request path '{path}' holds a '.' or '..' segment, which would address another resource.", nameof(pathAndQuery));
        }

        return new Uri(node, pathAndQuery);
    }

    internal static string Escape(string segment) => Uri.EscapeDataString(segment);
}
