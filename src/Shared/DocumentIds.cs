using FluentResults;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// Checks the ids of a bulk remove the same way in the service and every provider: the list and each id must be
/// set, and a repeated id is removed once.
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
    public static Error FailedError(string index, int removed, int requested, IReadOnlyCollection<string> failedIds, string reason)
    {
        var named = string.Join(", ", failedIds.Take(NamedIds));
        if (failedIds.Count > NamedIds)
        {
            named += $" and {failedIds.Count - NamedIds} more";
        }

        return new Error($"Removing documents from {index} failed for ids {named}: {reason}. {removed} of {requested} documents were removed.")
            .WithMetadata("ids", failedIds.ToList());
    }
}
