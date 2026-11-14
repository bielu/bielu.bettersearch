using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.Services;

namespace Bielu.BetterSearch.ElasticSearch.Configuration;

public class ElasticSearchOptions
{
    public static string ElasticSearchOptionsKey { get; } = "Bielu:BetterSearch:ElasticSearch";
    public ElasticSearchIndexSettings SharedIndexSettings { get; set; } = new();

    /// <summary>
    /// Put before every index name this provider sends to the server, so several applications or environments can share
    /// one cluster: with <c>app-</c>, the index <c>people</c> is <c>app-people</c> on the server. Indexing, search,
    /// removal, the index checks and rebuilds all add it, and callers keep using the name without it. Must be lower
    /// case, and can't start with <c>-</c>, <c>_</c> or <c>+</c>.
    /// </summary>
    public string? IndexPrefix { get; set; }

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
    /// The SHA-256 fingerprint of the server's certificate or of the CA that signed it, as hex, with or without colons,
    /// such as the one Elasticsearch prints on first start for its self-signed <c>http_ca.crt</c>. The server's certificate
    /// is then trusted when it or a certificate in its chain has this fingerprint. Like
    /// <see cref="DevelopmentOnlySkipCertificateValidation"/>, it goes with the endpoint: an <c>Indexes</c> entry with its own
    /// <c>ConnectionString</c>, or its own <c>CLOUD</c> or <c>CLOUDAPI</c> authentication, uses only its own TLS settings,
    /// and one without takes the shared ones.
    /// </summary>
    public string? CertificateFingerprint { get; set; }

    /// <summary>
    /// Accepts any server certificate, so anyone between the application and the server can read and change the traffic,
    /// credentials included. Only for a local development cluster with a self-signed certificate. A certificate the system
    /// trusts needs neither TLS setting; set <see cref="CertificateFingerprint"/> only for one it does not. It can't be set
    /// together with <see cref="CertificateFingerprint"/>.
    /// </summary>
    public bool? DevelopmentOnlySkipCertificateValidation { get; set; }

    /// <summary>
    /// Fields mapped explicitly when the index is created, or added to it by <c>EnsureIndexExistsAsync</c>. Fields not
    /// listed keep dynamic mapping. An <c>Indexes</c> entry adds to the shared mappings and replaces a shared mapping
    /// of the same field.
    /// </summary>
    public List<ElasticSearchFieldMapping>? Mappings { get; set; }

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
    PASSWORD,

    /// <summary>
    /// An API key sent to the cluster at <c>ConnectionString</c> as <c>Authorization: ApiKey &lt;key&gt;</c>. Set
    /// <c>AuthenticationDetails:ApiKey</c> to the <c>encoded</c> value that creating the key returns.
    /// </summary>
    APIKEY,

    /// <summary>
    /// A token sent to the cluster at <c>ConnectionString</c> as <c>Authorization: Bearer &lt;token&gt;</c>, such as a
    /// service account token or an access token from the token API. Set <c>AuthenticationDetails:Token</c>.
    /// </summary>
    BEARER
}
public class AuthenticationDetails
{
    public string Username { get; set; }
    public string Password { get; set; }
    public string ApiKey { get; set; }
    public string Id { get; set; }

    /// <summary>The token <see cref="AuthenticationType.BEARER"/> authentication sends.</summary>
    public string? Token { get; set; }
}
