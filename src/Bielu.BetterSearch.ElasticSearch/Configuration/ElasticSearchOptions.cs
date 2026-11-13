using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.Services;

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

    /// <summary>
    /// How to authenticate. Left unset on an <c>Indexes</c> entry, the index takes the shared authentication;
    /// set it to <see cref="Configuration.AuthenticationType.NONE"/> there to turn the shared authentication off.
    /// </summary>
    public AuthenticationType? AuthenticationType { get; set; }
    public AuthenticationDetails? AuthenticationDetails { get; set; }

    /// <summary>
    /// Fields mapped explicitly when the index is created, or added to it by <c>EnsureIndexExistsAsync</c>. Fields not
    /// listed keep dynamic mapping. An <c>Indexes</c> entry adds to the shared mappings and replaces a shared mapping
    /// of the same field.
    /// </summary>
    public List<ElasticSearchFieldMapping>? Mappings { get; set; }
}

/// <summary>An explicit mapping for one document field.</summary>
public class ElasticSearchFieldMapping : IFieldMapping
{
    /// <summary>The field's name as queries use it, such as <c>City</c>, without the <c>fields.</c> prefix.</summary>
    public string? Field { get; set; }

    public ElasticSearchFieldType? Type { get; set; }

    /// <summary>The analyzer of a <see cref="ElasticSearchFieldType.Text"/> field, such as <c>english</c>.</summary>
    public string? Analyzer { get; set; }

    /// <summary>The format of a <see cref="ElasticSearchFieldType.Date"/> field, such as <c>strict_date_optional_time</c>.</summary>
    public string? Format { get; set; }

    bool IFieldMapping.HasType => Type is { } type && Enum.IsDefined(type);
    bool IFieldMapping.IsText => Type == ElasticSearchFieldType.Text;
    bool IFieldMapping.IsDate => Type == ElasticSearchFieldType.Date;
}

/// <summary>The Elasticsearch field types a mapping can set.</summary>
public enum ElasticSearchFieldType
{
    /// <summary>Analyzed text, with a <c>keyword</c> sub-field that sorting uses.</summary>
    Text,

    /// <summary>The whole value, matched, sorted and aggregated as it is.</summary>
    Keyword,
    Date,
    Long,
    Integer,
    Short,
    Byte,
    Double,
    Float,
    Boolean,
    GeoPoint
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
