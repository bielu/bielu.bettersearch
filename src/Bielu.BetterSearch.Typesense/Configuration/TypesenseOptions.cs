using Bielu.BetterSearch.Abstractions.Configuration;

namespace Bielu.BetterSearch.Typesense.Configuration;

public class TypesenseOptions
{
    public static string TypesenseOptionsKey { get; } = "Bielu:BetterSearch:Typesense";
    public TypesenseIndexSettings SharedIndexSettings { get; set; } = new();

    /// <summary>
    /// Put before every collection name this provider sends to the server, so several applications or environments can
    /// share one server: with <c>app-</c>, the index <c>people</c> is the collection <c>app-people</c> on the server.
    /// Indexing, search, removal, the index checks and rebuilds all add it, and callers keep using the name without it.
    /// Typesense accepts any characters in a collection name, so the prefix is not restricted.
    /// </summary>
    public string? IndexPrefix { get; set; }

    private Dictionary<string, TypesenseIndexSettings>? _indexes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Settings for single collections, by index name. Names match whatever their case, as configuration keys do,
    /// so <c>Indexes:People</c> applies to the index <c>people</c>. An assigned dictionary is copied into a
    /// case-insensitive one; when two of its keys differ only in case, the last one wins.
    /// </summary>
    public Dictionary<string, TypesenseIndexSettings>? Indexes
    {
        get => _indexes;
        set => _indexes = value == null || value.Comparer == StringComparer.OrdinalIgnoreCase
            ? value
            : CaseInsensitiveCopy(value);
    }

    private static Dictionary<string, TypesenseIndexSettings> CaseInsensitiveCopy(Dictionary<string, TypesenseIndexSettings> indexes)
    {
        var copy = new Dictionary<string, TypesenseIndexSettings>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, settings) in indexes)
        {
            copy[name] = settings;
        }

        return copy;
    }

    /// <summary>
    /// How long one request to one node may take before the next node is tried, or the request fails.
    /// <c>TimeSpan.Zero</c> means no timeout. It must not be negative.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long a collection's list of fields, read by a search, is trusted before a search reads it again. A query on
    /// a field the list does not hold reads it again once the list is a second old, and a write through this provider
    /// makes the next search read it, so a field added by a write is missed for at most a second, and only when another
    /// process wrote it. <c>TimeSpan.Zero</c> reads it on every search.
    /// </summary>
    public TimeSpan SchemaCheckInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The document field holding each document's location, for <c>SpatialSearchQuery</c>. Typesense filters by
    /// distance only on a <c>geopoint</c> field, so the provider stores this field's point as one. Its value is one point:
    /// a <c>"lat,lon"</c> string, as Elasticsearch accepts for a <c>geo_point</c>, or a dictionary with <c>lat</c> and
    /// <c>lon</c> (or <c>lng</c>). Unset, spatial queries return a failed <c>Result</c>.
    /// </summary>
    public string? GeoField { get; set; }
}

public class TypesenseIndexSettings : IndexSettings
{
    /// <summary>The URL of the Typesense node, for example <c>http://localhost:8108</c>.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// The URLs of the cluster's other nodes. A request that cannot reach a node, or gets a 5xx answer from it, is
    /// sent to the next one, starting from <see cref="ConnectionString"/>.
    /// </summary>
    public IList<string>? AdditionalNodes { get; set; }

    /// <summary>The admin key, or a key allowed to manage the collections the provider uses.</summary>
    public string? ApiKey { get; set; }
}
