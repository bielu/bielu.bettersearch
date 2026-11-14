namespace Bielu.BetterSearch.Abstractions.Configuration;

/// <summary>When a successful write becomes visible to searches.</summary>
public enum WriteVisibility
{
    /// <summary>
    /// The engine's own behavior. Elasticsearch and OpenSearch make a write searchable at the next index refresh,
    /// about a second later by default; Meilisearch and Lifti already return only once it is searchable.
    /// </summary>
    Default,

    /// <summary>
    /// A successful write returns only once a search can see it. Elasticsearch and OpenSearch wait for the next
    /// refresh (<c>refresh=wait_for</c>), or refresh at once when removing every document (<c>refresh=true</c>).
    /// </summary>
    WaitUntilSearchable
}
