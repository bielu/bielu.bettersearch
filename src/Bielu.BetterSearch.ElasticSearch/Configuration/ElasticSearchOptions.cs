using Bielu.BetterSearch.Abstractions.Configuration;

namespace Bielu.BetterSearch.ElasticSearch.Configuration;

public class ElasticSearchOptions
{
    public static string ElasticSearchOptionsKey { get; } = "Bielu:BetterSearch:ElasticSearch";
    public ElasticSearchIndexSettings SharedIndexSettings { get; set; }
    private Dictionary<string, ElasticSearchIndexSettings>? _indexes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Settings for single indexes, by index name. Names match whatever their case, as configuration keys do,
    /// so <c>Indexes:People</c> applies to the index <c>people</c>. An assigned dictionary is copied into a
    /// case-insensitive one; when two of its keys differ only in case, the last one wins.
    /// </summary>
    public Dictionary<string, ElasticSearchIndexSettings>? Indexes
    {
        get => _indexes;
        set => _indexes = value == null || value.Comparer == StringComparer.OrdinalIgnoreCase
            ? value
            : CaseInsensitiveCopy(value);
    }

    private static Dictionary<string, ElasticSearchIndexSettings> CaseInsensitiveCopy(Dictionary<string, ElasticSearchIndexSettings> indexes)
    {
        var copy = new Dictionary<string, ElasticSearchIndexSettings>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, settings) in indexes)
        {
            copy[name] = settings;
        }

        return copy;
    }

    /// <summary>
    /// The deepest result a search may page to, <c>(Page - 1) * PageSize + PageSize</c>. Match it to the
    /// indexes' <c>index.max_result_window</c>, which is 10,000 unless changed.
    /// </summary>
    public int MaxResultWindow { get; set; } = 10_000;

}

public class ElasticSearchIndexSettings : IndexSettings
{
    public string ConnectionString { get; set; }
    public AuthenticationType AuthenticationType { get; set; }
    public AuthenticationDetails? AuthenticationDetails { get; set; }
}
public enum AuthenticationType
{
    NONE,
    CLOUD,
    CLOUDAPI,
    PASSWORD
}
public class AuthenticationDetails
{
    public string Username { get; set; }
    public string Password { get; set; }
    public string ApiKey { get; set; }
    public string Id { get; set; }
}
