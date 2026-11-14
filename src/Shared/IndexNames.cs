namespace Bielu.BetterSearch.Services;

/// <summary>
/// How a provider names indexes on the engine: the configured prefix and the index name, and the new indexes a rebuild
/// creates, so indexing, search and the index lifecycle agree on every name.
/// </summary>
internal static class IndexNames
{
    private const string RebuildMarker = "-rebuild-";

    // Characters Elasticsearch and OpenSearch reject anywhere in an index name.
    private static readonly char[] InvalidCharacters = ['\\', '/', '*', '?', '"', '<', '>', '|', ',', '#', ':', ' '];

    /// <summary>The name the engine knows <paramref name="index"/> by: <paramref name="prefix"/> followed by it.</summary>
    public static string WithPrefix(string? prefix, string index) => string.IsNullOrEmpty(prefix) ? index : prefix + index;

    /// <summary>
    /// A new, unique name for a rebuild of <paramref name="engineIndex"/>, ordered by when the rebuild started:
    /// the name, <c>-rebuild-</c>, the UTC time to the millisecond and a random suffix, so two rebuilds started in the
    /// same millisecond still get different names.
    /// </summary>
    public static string NewRebuildTarget(string engineIndex, DateTimeOffset now) =>
        $"{engineIndex}{RebuildMarker}{now.UtcDateTime:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..(engineIndex.Length + RebuildMarker.Length + 17 + 1 + 8)];

    /// <summary>
    /// The alias a rebuild index carries from its creation until the rebuild is completed or aborted. Completing removes
    /// it in the same atomic request that moves the index alias, and aborting removes it before deleting the index, each
    /// requiring it to exist, so once one of them has removed it the other fails rather than acting on a stale check.
    /// </summary>
    public static string RebuildPendingAlias(string target) => target + "-pending";

    /// <summary>Whether <paramref name="target"/> is a name <see cref="NewRebuildTarget"/> gave a rebuild of <paramref name="engineIndex"/>.</summary>
    public static bool IsRebuildTargetOf(string engineIndex, string? target)
    {
        var start = engineIndex + RebuildMarker;
        if (target is null || !target.StartsWith(start, StringComparison.Ordinal))
        {
            return false;
        }

        // The rest is exactly what NewRebuildTarget writes: 17 digits, a dash and 8 hex digits.
        var rest = target.AsSpan(start.Length);
        return rest.Length == 26
            && rest[17] == '-'
            && !rest[..17].ContainsAnyExceptInRange('0', '9')
            && rest[18..].IndexOfAnyExcept("0123456789abcdef") < 0;
    }

    /// <summary>The problems with an index prefix that Elasticsearch or OpenSearch would reject, each naming <paramref name="key"/>.</summary>
    public static IEnumerable<string> ValidatePrefix(string key, string? prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            yield break;
        }

        if (!string.Equals(prefix, prefix.ToLowerInvariant(), StringComparison.Ordinal))
        {
            yield return $"{key} '{prefix}' has upper case letters; index names must be lower case.";
        }

        if (prefix.IndexOfAny(InvalidCharacters) >= 0)
        {
            yield return $"{key} '{prefix}' has a character index names can't contain: {string.Join(" ", InvalidCharacters.Select(c => c == ' ' ? "space" : c.ToString()))}.";
        }

        if (prefix[0] is '-' or '_' or '+')
        {
            yield return $"{key} '{prefix}' starts with '{prefix[0]}'; index names can't start with -, _ or +.";
        }
    }
}

/// <summary>Checks a batch for <c>IndexRebuildDocumentsAsync</c>, which every provider applies the same way.</summary>
internal static class RebuildDocuments
{
    /// <summary>Fails when a document is <c>null</c> or is for another index than the one being rebuilt.</summary>
    public static FluentResults.Result Check(Abstractions.Models.IndexRebuild rebuild, IReadOnlyList<SearchDocument?> documents)
    {
        if (documents.Any(document => document is null))
        {
            return FluentResults.Result.Fail("A document is null.");
        }

        var other = documents.FirstOrDefault(document => !string.Equals(document!.Index, rebuild.Index, StringComparison.OrdinalIgnoreCase));
        return other is null
            ? FluentResults.Result.Ok()
            : FluentResults.Result.Fail($"Document {other.Id} is for index '{other.Index}', not '{rebuild.Index}', the index being rebuilt.");
    }
}
