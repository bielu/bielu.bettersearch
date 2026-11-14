using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bielu.BetterSearch.Diagnostics;

/// <summary>
/// The span, metrics and log line of one service call. The tags hold the operation, the index, the provider and
/// counts only, never query values or document contents. A failure is recorded with its message as the provider or
/// engine wrote it, which can quote the query (a Lucene syntax error quotes the whole query).
/// </summary>
internal static partial class BetterSearchTelemetry
{
    internal const string OperationTag = "db.operation.name";
    internal const string IndexTag = "db.collection.name";
    internal const string ProviderTag = "bettersearch.provider";
    internal const string OutcomeTag = "bettersearch.outcome";
    internal const string PageTag = "bettersearch.page";
    internal const string PageSizeTag = "bettersearch.page_size";
    internal const string HitCountTag = "db.response.returned_rows";
    internal const string TotalCountTag = "bettersearch.total_count";
    internal const string DocumentCountTag = "bettersearch.document_count";
    internal const string ErrorTypeTag = "error.type";

    internal const string Success = "success";
    internal const string Failure = "failure";
    internal const string Cancelled = "cancelled";
    internal const string Exception = "exception";

    private static readonly string? Version = typeof(BetterSearchTelemetry).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    internal static readonly ActivitySource ActivitySource = new(BetterSearchInstrumentation.ActivitySourceName, Version);
    internal static readonly Meter Meter = new(BetterSearchInstrumentation.MeterName, Version);

    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "bettersearch.operation.duration", "s", "Duration of BetterSearch service calls.");
    private static readonly Counter<long> Operations = Meter.CreateCounter<long>(
        "bettersearch.operations", "{operation}", "BetterSearch service calls, by outcome.");
    private static readonly Counter<long> DocumentsIndexed = Meter.CreateCounter<long>(
        "bettersearch.documents.indexed", "{document}", "Documents the provider reported as indexed.");
    private static readonly Counter<long> DocumentsRemoved = Meter.CreateCounter<long>(
        "bettersearch.documents.removed", "{document}", "Documents the provider reported as removed.");

    /// <summary>
    /// Runs <paramref name="work"/> inside a span named <c>{operation} {index}</c>, then records its duration and outcome.
    /// A failed <see cref="Result"/> sets the span status to error with the first error's message and is logged at
    /// <c>Warning</c>; a cancellation is recorded as <c>cancelled</c> and not logged; any other exception is logged at
    /// <c>Error</c> and rethrown.
    /// </summary>
    internal static async Task<TResult> RunAsync<TResult>(
        string operation,
        string? index,
        object provider,
        ILogger? logger,
        Action<Activity>? tagRequest,
        Func<Task<TResult>> work,
        Action<Activity, TResult>? tagResult = null)
        where TResult : IResultBase
    {
        var providerName = provider.GetType().Name;
        var startTimestamp = Stopwatch.GetTimestamp();
        using var activity = ActivitySource.StartActivity(
            string.IsNullOrEmpty(index) ? operation : $"{operation} {index}", ActivityKind.Client);
        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(OperationTag, operation);
            activity.SetTag(IndexTag, index);
            activity.SetTag(ProviderTag, providerName);
            tagRequest?.Invoke(activity);
        }

        var outcome = Exception;
        try
        {
            var result = await work().ConfigureAwait(false);
            outcome = result.IsSuccess ? Success : Failure;
            if (activity is { IsAllDataRequested: true })
            {
                activity.SetTag(OutcomeTag, outcome);
                if (result.IsSuccess)
                {
                    tagResult?.Invoke(activity, result);
                }
                else
                {
                    activity.SetTag(ErrorTypeTag, Failure);
                    activity.SetStatus(ActivityStatusCode.Error, FirstMessage(result));
                }
            }

            if (result.IsFailed)
            {
                LogFailedResult(logger ?? NullLogger.Instance, operation, index, providerName, FirstMessage(result));
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            outcome = Cancelled;
            activity?.SetTag(OutcomeTag, outcome);
            activity?.SetTag(ErrorTypeTag, nameof(OperationCanceledException));
            activity?.SetStatus(ActivityStatusCode.Error, "The operation was cancelled.");
            throw;
        }
        catch (Exception e)
        {
            activity?.SetTag(OutcomeTag, outcome);
            activity?.SetTag(ErrorTypeTag, e.GetType().FullName);
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            LogException(logger ?? NullLogger.Instance, e, operation, index, providerName);
            throw;
        }
        finally
        {
            var tags = new TagList
            {
                { OperationTag, operation },
                { IndexTag, index },
                { ProviderTag, providerName },
                { OutcomeTag, outcome },
            };
            Duration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, tags);
            Operations.Add(1, tags);
        }
    }

    internal static void RecordIndexed(long count, string? index, object provider) =>
        DocumentsIndexed.Add(count, DocumentTags(index, provider));

    internal static void RecordRemoved(long count, string? index, object provider) =>
        DocumentsRemoved.Add(count, DocumentTags(index, provider));

    private static TagList DocumentTags(string? index, object provider) => new()
    {
        { IndexTag, index },
        { ProviderTag, provider.GetType().Name },
    };

    private static string FirstMessage(IResultBase result) =>
        result.Errors.Count > 0 ? result.Errors[0].Message : "The operation failed.";

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "BetterSearch {Operation} on index '{Index}' with {Provider} returned a failed result: {Error}")]
    private static partial void LogFailedResult(ILogger logger, string operation, string? index, string provider, string error);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error,
        Message = "BetterSearch {Operation} on index '{Index}' with {Provider} threw")]
    private static partial void LogException(ILogger logger, Exception exception, string operation, string? index, string provider);
}
