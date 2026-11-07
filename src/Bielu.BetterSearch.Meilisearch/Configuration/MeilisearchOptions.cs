using Bielu.BetterSearch.Abstractions.Configuration;

namespace Bielu.BetterSearch.Meilisearch.Configuration;

public class MeilisearchOptions
{
    public static string MeilisearchOptionsKey { get; } = "Bielu:BetterSearch:Meilisearch";
    public MeilisearchIndexSettings SharedIndexSettings { get; set; } = new();
    public Dictionary<string, MeilisearchIndexSettings>? Indexes { get; set; }

    /// <summary>
    /// How long to wait for Meilisearch to finish a write. Meilisearch queues every write as a task;
    /// the provider waits for it so a successful <c>Result</c> means the write was applied. A timeout fails the
    /// <c>Result</c> without cancelling the task, so the write may still be applied later.
    /// </summary>
    public TimeSpan TaskTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The deepest result a search may page to, <c>Page * PageSize</c>. The provider sets it as each index's
    /// <c>pagination.maxTotalHits</c> when it writes to the index; 10,000 matches Elasticsearch's default window.
    /// </summary>
    public int MaxTotalHits { get; set; } = 10_000;
}

public class MeilisearchIndexSettings : IndexSettings
{
    /// <summary>The URL of the Meilisearch server, for example <c>http://localhost:7700</c>.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>The master key or an API key; empty when the server runs without one.</summary>
    public string? ApiKey { get; set; }
}
