using FluentResults;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// Sends one index's documents in bulk requests of at most <c>BulkBatchSize</c> documents each, so a large import
/// stays under the engine's request size limit, and merges the requests' results into one, as a single request would
/// report them. For engines that apply each bulk item on its own, such as Elasticsearch and OpenSearch; Meilisearch
/// uses only <see cref="Size"/>, as it applies or fails each batch as a whole.
/// </summary>
internal static class BulkBatches
{
    public const int DefaultBatchSize = 1000;

    /// <summary>What one bulk request returned: an error when it failed as a whole, or else the items it rejected.</summary>
    public sealed record Outcome(Error? RequestError, IReadOnlyList<string> FailedIds, string? Reason)
    {
        public static Outcome Failed(Error requestError) => new(requestError, [], null);

        public static Outcome Items(IReadOnlyList<string> failedIds, string? reason) => new(null, failedIds, reason);
    }

    /// <summary>
    /// The number of documents to send in one request: <paramref name="batchSize"/>, or 1000 when unset. A value of 0 or
    /// less fails, naming the setting, for options built in code that skip the startup validation.
    /// </summary>
    public static Result<int> Size(string index, int? batchSize, string optionsKey)
    {
        var size = batchSize ?? DefaultBatchSize;
        return size > 0
            ? Result.Ok(size)
            : Result.Fail<int>($"BulkBatchSize is {size} for index {index}, but it must be greater than 0. Set {optionsKey}:SharedIndexSettings:BulkBatchSize, or BulkBatchSize on the index's Indexes entry.");
    }

    /// <summary>
    /// Sends <paramref name="documents"/> in requests of <paramref name="batchSize"/> documents (1000 when unset) and
    /// returns how many were indexed. Rejected items fail with every rejected id, after every request was sent. A
    /// request that fails as a whole stops the import: when it is the first, its error is returned as it is, and
    /// otherwise the error names every document not confirmed as stored.
    /// </summary>
    public static async Task<Result<int>> IndexAsync(string index, IReadOnlyList<SearchDocument> documents, int? batchSize,
        string optionsKey, Func<SearchDocument[], Task<Outcome>> send)
    {
        var resolved = Size(index, batchSize, optionsKey);
        if (resolved.IsFailed)
        {
            return resolved;
        }

        var size = resolved.Value;
        var sent = 0;
        var failedIds = new List<string>();
        string? reason = null;
        foreach (var batch in documents.Chunk(size))
        {
            var outcome = await send(batch).ConfigureAwait(false);
            if (outcome.RequestError is { } requestError)
            {
                if (sent == 0)
                {
                    return Result.Fail<int>(requestError);
                }

                var notSent = documents.Count - sent - batch.Length;
                var unconfirmed = failedIds.Concat(documents.Skip(sent).Select(document => document.Id)).ToList();
                return Result.Fail<int>(DocumentIds.IndexFailedError(index, sent - failedIds.Count, documents.Count, unconfirmed,
                        $"{requestError.Message}. That was the bulk request for documents {sent + 1} to {sent + batch.Length}, which may or may not be stored, and the {notSent} documents after them were not sent")
                    .CausedBy(requestError));
            }

            failedIds.AddRange(outcome.FailedIds);
            reason ??= outcome.Reason;
            sent += batch.Length;
        }

        return failedIds.Count == 0
            ? Result.Ok(sent)
            : Result.Fail<int>(DocumentIds.IndexFailedError(index, sent - failedIds.Count, documents.Count, failedIds, reason ?? "rejected"));
    }
}
