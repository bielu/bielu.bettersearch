using Bielu.BetterSearch.Abstractions.Configuration;

namespace Bielu.BetterSearch.PostgreSql.Configuration;

public class PostgreSqlOptions
{
    public static string PostgreSqlOptionsKey { get; } = "Bielu:BetterSearch:PostgreSql";
    public PostgreSqlIndexSettings SharedIndexSettings { get; set; } = new();
    private Dictionary<string, PostgreSqlIndexSettings>? _indexes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Settings for single indexes, by index name. Names match whatever their case, as configuration keys do,
    /// so <c>Indexes:People</c> applies to the index <c>people</c>. An assigned dictionary is copied into a
    /// case-insensitive one; when two of its keys differ only in case, the last one wins.
    /// </summary>
    public Dictionary<string, PostgreSqlIndexSettings>? Indexes
    {
        get => _indexes;
        set => _indexes = value == null || value.Comparer == StringComparer.OrdinalIgnoreCase
            ? value
            : CaseInsensitiveCopy(value);
    }

    private static Dictionary<string, PostgreSqlIndexSettings> CaseInsensitiveCopy(Dictionary<string, PostgreSqlIndexSettings> indexes)
    {
        var copy = new Dictionary<string, PostgreSqlIndexSettings>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, settings) in indexes)
        {
            copy[name] = settings;
        }

        return copy;
    }

    /// <summary>
    /// The schema holding the provider's tables, one per index. A schema other than <c>public</c> is created by the
    /// first <c>CreateIndexAsync</c> when it is missing.
    /// </summary>
    public string Schema { get; set; } = "public";

    /// <summary>
    /// The text search configuration that splits string values into words, for example <c>simple</c> (lower-cased
    /// words, no stemming, the closest to Elasticsearch's standard analyzer) or <c>english</c> (stemming and stop
    /// words). It decides what a term, prefix or fuzzy query matches. A table stores its words with the configuration
    /// it was created with, so after changing it, rebuild the existing indexes.
    /// </summary>
    public string TextSearchConfiguration { get; set; } = "simple";
}

public class PostgreSqlIndexSettings : IndexSettings
{
    /// <summary>
    /// The Npgsql connection string, for example <c>Host=localhost;Database=search;Username=app;Password=...</c>.
    /// Leave it empty to use the <c>NpgsqlDataSource</c> registered in the service collection, for example by
    /// <c>AddNpgsqlDataSource</c>.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
}
