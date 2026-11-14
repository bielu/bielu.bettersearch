using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.Services;

namespace Bielu.BetterSearch.OpenSearch.Configuration;

public class OpenSearchOptions
{
    public static string OpenSearchOptionsKey { get; } = "Bielu:BetterSearch:OpenSearch";
    public OpenSearchIndexSettings SharedIndexSettings { get; set; } = new();

    /// <summary>
    /// Put before every index name this provider sends to the server, so several applications or environments can share
    /// one cluster: with <c>app-</c>, the index <c>people</c> is <c>app-people</c> on the server. Indexing, search,
    /// removal, the index checks and rebuilds all add it, and callers keep using the name without it. Must be lower
    /// case, and can't start with <c>-</c>, <c>_</c> or <c>+</c>.
    /// </summary>
    public string? IndexPrefix { get; set; }

    private Dictionary<string, OpenSearchIndexSettings>? _indexes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Settings for single indexes, by index name. Names match whatever their case, as configuration keys do,
    /// so <c>Indexes:People</c> applies to the index <c>people</c>. An assigned dictionary is copied into a
    /// case-insensitive one; when two of its keys differ only in case, the last one wins.
    /// </summary>
    public Dictionary<string, OpenSearchIndexSettings>? Indexes
    {
        get => _indexes;
        set => _indexes = value == null || value.Comparer == StringComparer.OrdinalIgnoreCase
            ? value
            : CaseInsensitiveCopy(value);
    }

    private static Dictionary<string, OpenSearchIndexSettings> CaseInsensitiveCopy(Dictionary<string, OpenSearchIndexSettings> indexes)
    {
        var copy = new Dictionary<string, OpenSearchIndexSettings>(StringComparer.OrdinalIgnoreCase);
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

public class OpenSearchIndexSettings : IndexSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    /// <summary>
    /// How to authenticate. Unset under <c>Indexes</c> means the index takes the shared authentication;
    /// <c>NONE</c> there turns it off for that index. Unset in <c>SharedIndexSettings</c> means <c>NONE</c>.
    /// </summary>
    public OpenSearchAuthenticationType? AuthenticationType { get; set; }
    public OpenSearchAuthenticationDetails? AuthenticationDetails { get; set; }

    /// <summary>
    /// The SHA-256 fingerprint of the server's certificate or of the CA that signed it, as hex, with or without colons.
    /// A certificate the system does not trust is then accepted when it or a certificate in its chain has this
    /// fingerprint, whatever host name it names. Like <see cref="DevelopmentOnlySkipCertificateValidation"/>, it goes with
    /// the connection string: an <c>Indexes</c> entry with its own <c>ConnectionString</c> uses only its own TLS settings,
    /// and one without takes the shared ones.
    /// </summary>
    public string? CertificateFingerprint { get; set; }

    /// <summary>
    /// Accepts any server certificate, so anyone between the application and the server can read and change the traffic,
    /// credentials included. Only for a local development cluster with a self-signed certificate; set
    /// <see cref="CertificateFingerprint"/> instead everywhere else. It can't be set together with <see cref="CertificateFingerprint"/>.
    /// </summary>
    public bool? DevelopmentOnlySkipCertificateValidation { get; set; }

    /// <summary>
    /// Fields mapped explicitly when the index is created, or added to it by <c>EnsureIndexExistsAsync</c>. Fields not
    /// listed keep dynamic mapping. An <c>Indexes</c> entry adds to the shared mappings and replaces a shared mapping
    /// of the same field.
    /// </summary>
    public List<OpenSearchFieldMapping>? Mappings { get; set; }

    /// <summary>
    /// How many documents <c>IndexMultipleDocumentsAsync</c> sends in one bulk request; a larger batch is split into
    /// several requests, so it stays under the cluster's <c>http.max_content_length</c> (100 MB unless changed). Unset
    /// on an <c>Indexes</c> entry, the index takes the shared value, and unset there means 1000. It must be greater than 0.
    /// </summary>
    public int? BulkBatchSize { get; set; }

    /// <summary>
    /// When a successful index or remove call returns. <see cref="Abstractions.Configuration.WriteVisibility.WaitUntilSearchable"/>
    /// waits until a search can see the write, at the cost of a slower call. Left unset on an <c>Indexes</c> entry, the
    /// index takes the shared setting; set it to <see cref="Abstractions.Configuration.WriteVisibility.Default"/> there
    /// to turn the shared one off.
    /// </summary>
    public WriteVisibility? WriteVisibility { get; set; }
}

/// <summary>An explicit mapping for one document field.</summary>
public class OpenSearchFieldMapping : IFieldMapping
{
    /// <summary>The field's name as queries use it, such as <c>City</c>, without the <c>fields.</c> prefix.</summary>
    public string? Field { get; set; }

    public OpenSearchFieldType? Type { get; set; }

    /// <summary>The analyzer of a <see cref="OpenSearchFieldType.Text"/> field, such as <c>english</c>.</summary>
    public string? Analyzer { get; set; }

    /// <summary>The format of a <see cref="OpenSearchFieldType.Date"/> field, such as <c>strict_date_optional_time</c>.</summary>
    public string? Format { get; set; }

    bool IFieldMapping.HasType => Type is { } type && Enum.IsDefined(type);
    bool IFieldMapping.IsText => Type == OpenSearchFieldType.Text;
    bool IFieldMapping.IsDate => Type == OpenSearchFieldType.Date;
}

/// <summary>The OpenSearch field types a mapping can set.</summary>
public enum OpenSearchFieldType
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

public enum OpenSearchAuthenticationType
{
    NONE = 0,
    PASSWORD = 1,
    APIKEY = 2
}

public class OpenSearchAuthenticationDetails
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>An OpenSearch API key (3.x security plugin), sent as <c>Authorization: ApiKey &lt;key&gt;</c>.</summary>
    public string ApiKey { get; set; } = string.Empty;
}
