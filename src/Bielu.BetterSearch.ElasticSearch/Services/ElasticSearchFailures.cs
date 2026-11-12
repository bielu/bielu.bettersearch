using Elastic.Transport.Products.Elasticsearch;

namespace Bielu.BetterSearch.ElasticSearch.Services;

/// <summary>Short messages for failed Elasticsearch responses, in place of the multi-line <c>DebugInformation</c> audit trail.</summary>
internal static class ElasticSearchFailures
{
    /// <summary>
    /// The HTTP status when there is one, then the server's error when it sent one, otherwise the transport's exception,
    /// and <c>DebugInformation</c> only when there is neither.
    /// </summary>
    internal static string Describe(string operation, ElasticsearchResponse response)
    {
        var status = response.ApiCallDetails?.HttpStatusCode;
        var failed = status is null ? $"Elasticsearch {operation} failed" : $"Elasticsearch {operation} failed with HTTP {status}";
        var error = response.ElasticsearchServerError?.Error;
        if (error == null)
        {
            return $"{failed}: {response.ApiCallDetails?.OriginalException?.Message ?? response.DebugInformation}";
        }

        var cause = error.RootCause?.FirstOrDefault(rootCause => rootCause.Reason != error.Reason)?.Reason;
        return cause == null
            ? $"{failed} ({error.Type}): {error.Reason}"
            : $"{failed} ({error.Type}): {error.Reason}: {cause}";
    }
}
