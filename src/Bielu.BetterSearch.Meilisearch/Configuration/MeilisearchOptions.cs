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

    /// <summary>
    /// The document field holding each document's location, for <c>SpatialSearchQuery</c>. Meilisearch only filters by
    /// distance on its top-level <c>_geo</c> attribute, so the provider copies this field there when it indexes a
    /// document. Its value is one point: a <c>"lat,lon"</c> string, as Elasticsearch accepts for a <c>geo_point</c>, or a
    /// dictionary with <c>lat</c> and <c>lon</c> (or <c>lng</c>). Unset, spatial queries return a failed <c>Result</c>.
    /// </summary>
    public string? GeoField { get; set; }

    /// <summary>
    /// The fields a <c>PrefixPhraseSubQuery</c> can search. For each word of a text value the provider stores the run of
    /// up to 16 words starting there, so a long text stores about 16 times its words. Listing the fields that need
    /// phrase prefixes stores runs for those fields only, and a phrase prefix on any other field returns a failed
    /// <c>Result</c>. Unset, every field stores them.
    /// </summary>
    public IList<string>? PhraseFields { get; set; }
}

public class MeilisearchIndexSettings : IndexSettings
{
    /// <summary>The URL of the Meilisearch server, for example <c>http://localhost:7700</c>.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>The master key or an API key; empty when the server runs without one.</summary>
    public string? ApiKey { get; set; }
}
