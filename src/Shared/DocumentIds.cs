using FluentResults;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// Checks the ids of a bulk remove the same way in the service and every provider: the list and each id must be
/// set, and a repeated id is removed once. Also builds the error each provider returns for the ids a bulk write failed.
/// </summary>
internal static class DocumentIds
{
    public static Result<IReadOnlyList<string>> Distinct(IEnumerable<string>? ids)
    {
        if (ids is null)
        {
            return Result.Fail<IReadOnlyList<string>>("The ids to remove are null; pass an empty list to remove nothing.");
        }

        var distinct = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id))
            {
                return Result.Fail<IReadOnlyList<string>>("The ids to remove contain a null or empty id.");
            }

            if (seen.Add(id))
            {
                distinct.Add(id);
            }
        }

        return Result.Ok<IReadOnlyList<string>>(distinct);
    }

    /// <summary>How many failed ids the message names; the error's <c>ids</c> metadata holds all of them.</summary>
    private const int NamedIds = 20;

    /// <summary>
    /// The error for ids the engine failed to remove. The message names up to 20 of them and the <c>ids</c> metadata
    /// holds every one.
    /// </summary>
    public static Error FailedError(string index, int removed, int requested, IReadOnlyCollection<string> failedIds, string reason) =>
        new Error($"Removing documents from {index} failed for ids {Named(failedIds)}: {reason}. {removed} of {requested} documents were removed.")
            .WithMetadata("ids", failedIds.ToList());

    /// <summary>
    /// The error for documents the engine failed to index. The message names up to 20 of their ids and the
    /// <c>ids</c> metadata holds every one.
    /// </summary>
    public static Error IndexFailedError(string index, int indexed, int requested, IReadOnlyCollection<string> failedIds, string reason) =>
        new Error($"Indexing documents into {index} failed for ids {Named(failedIds)}: {reason}. {indexed} of {requested} documents were indexed.")
            .WithMetadata("ids", failedIds.ToList());

    private static string Named(IReadOnlyCollection<string> ids)
    {
        var named = string.Join(", ", ids.Take(NamedIds));
        if (ids.Count > NamedIds)
        {
            named += $" and {ids.Count - NamedIds} more";
        }

        return named;
    }
}
