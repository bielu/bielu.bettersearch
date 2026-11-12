using OpenSearch.Client;

namespace Bielu.BetterSearch.OpenSearch.Services;

/// <summary>Short messages for failed OpenSearch responses, in place of the multi-line <c>DebugInformation</c> audit trail.</summary>
internal static class OpenSearchFailures
{
    /// <summary>
    /// The HTTP status when there is one, then the server's error when it sent one, otherwise the transport's exception,
    /// and <c>DebugInformation</c> only when there is neither.
    /// </summary>
    internal static string Describe(string operation, IResponse response)
    {
        var status = response.ApiCall?.HttpStatusCode;
        var failed = status is null ? $"OpenSearch {operation} failed" : $"OpenSearch {operation} failed with HTTP {status}";
        var error = response.ServerError?.Error;
        if (error == null)
        {
            return $"{failed}: {response.OriginalException?.Message ?? response.DebugInformation}";
        }

        var cause = error.RootCause?.FirstOrDefault(rootCause => rootCause.Reason != error.Reason)?.Reason;
        return cause == null
            ? $"{failed} ({error.Type}): {error.Reason}"
            : $"{failed} ({error.Type}): {error.Reason}: {cause}";
    }
}
