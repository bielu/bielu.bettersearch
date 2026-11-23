using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.Services;

namespace Bielu.BetterSearch.OpenSearch.Configuration;

/// <summary>The OpenSearch provider's options, bound from the <see cref="OpenSearchOptionsKey"/> configuration section.</summary>
public class OpenSearchOptions
{
    /// <summary>The configuration section the options are bound from, <c>Bielu:BetterSearch:OpenSearch</c>.</summary>
    public static string OpenSearchOptionsKey { get; } = "Bielu:BetterSearch:OpenSearch";
    /// <summary>The settings every index uses; an <see cref="Indexes"/> entry takes whatever it leaves unset from these.</summary>
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

/// <summary>Connection and index settings, shared by every index or set for one index in <see cref="OpenSearchOptions.Indexes"/>.</summary>
public class OpenSearchIndexSettings
{
    /// <summary>
    /// The cluster URL, an absolute <c>http</c> or <c>https</c> URL such as <c>http://localhost:9200</c>. Left unset on an
    /// <c>Indexes</c> entry, the index takes the shared one.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// The URLs of the cluster's other nodes, beside <see cref="ConnectionString"/>. A request that cannot reach a node
    /// is sent to the next one. Left unset on an <c>Indexes</c> entry that takes the shared connection string, the
    /// index takes the shared nodes; an entry with its own connection string uses only its own.
    /// </summary>
    public IList<string>? AdditionalNodes { get; set; }

    /// <summary>
    /// How the client spreads requests over the nodes. Unset, a single <see cref="ConnectionString"/> is the only node
    /// the client uses, as before, and with <see cref="AdditionalNodes"/> the nodes take turns as with
    /// <see cref="Abstractions.Configuration.NodePoolType.Static"/>. Left unset on an <c>Indexes</c> entry, the index
    /// takes the shared setting.
    /// </summary>
    public NodePoolType? PoolType { get; set; }
    /// <summary>
    /// How to authenticate. Unset under <c>Indexes</c> means the index takes the shared authentication;
    /// <c>None</c> there turns it off for that index. Unset in <c>SharedIndexSettings</c> means <c>None</c>.
    /// </summary>
    public OpenSearchAuthenticationType? AuthenticationType { get; set; }
    /// <summary>
    /// The credentials <see cref="AuthenticationType"/> uses. An <c>Indexes</c> entry uses its own only together with its own
    /// <see cref="AuthenticationType"/>, and otherwise takes the shared ones.
    /// </summary>
    public OpenSearchAuthenticationDetails? AuthenticationDetails { get; set; }

    /// <summary>
    /// The SHA-256 fingerprint of the server's certificate or of the CA that signed it, as hex, with or without colons.
    /// A certificate the system does not trust is then accepted when it has this fingerprint, or chains through valid
    /// signatures to a CA that has it, whatever host name it names. Like <see cref="DevelopmentOnlySkipCertificateValidation"/>, it goes with
    /// the connection string: an <c>Indexes</c> entry with its own <c>ConnectionString</c> uses only its own TLS settings,
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

    /// <summary>
    /// How long a request may take before the client gives up on it, set as the client's <c>RequestTimeout</c>. The client
    /// also limits a request's retries to this time unless its <c>MaxRetryTimeout</c> is set, which the
    /// <c>configureClient</c> callback can do. Unset on an <c>Indexes</c> entry, the index takes the shared value, and unset
    /// there leaves the client's default of 60 seconds. It must be greater than 0.
    /// </summary>
    public TimeSpan? RequestTimeout { get; set; }

    /// <summary>
    /// How many times the client retries a failed request, set as the client's <c>MaximumRetries</c>.
    /// Unset on an <c>Indexes</c> entry, the index takes the shared value, and unset there leaves the client's default.
    /// It must not be negative.
    /// </summary>
    public int? MaxRetries { get; set; }
}

/// <summary>An explicit mapping for one document field.</summary>
public class OpenSearchFieldMapping : IFieldMapping
{
    /// <summary>The field's name as queries use it, such as <c>City</c>, without the <c>fields.</c> prefix.</summary>
    public string? Field { get; set; }

    /// <summary>The field's OpenSearch type. Required: a mapping without it fails options validation.</summary>
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
    /// <summary>A date, written by the provider as ISO 8601 text; <see cref="OpenSearchFieldMapping.Format"/> can set its format.</summary>
    Date,
    /// <summary>A signed 64-bit integer.</summary>
    Long,
    /// <summary>A signed 32-bit integer.</summary>
    Integer,
    /// <summary>A signed 16-bit integer.</summary>
    Short,
    /// <summary>A signed 8-bit integer.</summary>
    Byte,
    /// <summary>A double-precision floating point number.</summary>
    Double,
    /// <summary>A single-precision floating point number.</summary>
    Float,
    /// <summary><c>true</c> or <c>false</c>.</summary>
    Boolean,
    /// <summary>A latitude and longitude, which <c>SpatialSearchSubQuery</c> needs.</summary>
    GeoPoint
}

/// <summary>How the OpenSearch provider authenticates to the cluster.</summary>
public enum OpenSearchAuthenticationType
{
    /// <summary>No authentication.</summary>
    None = 0,
    /// <summary>Basic authentication with <see cref="OpenSearchAuthenticationDetails.Username"/> and <see cref="OpenSearchAuthenticationDetails.Password"/>.</summary>
    Password = 1,
    /// <summary>An OpenSearch 3.x API key from <see cref="OpenSearchAuthenticationDetails.ApiKey"/>, sent as <c>Authorization: ApiKey &lt;key&gt;</c>.</summary>
    ApiKey = 2
}

/// <summary>The credentials of an <see cref="OpenSearchAuthenticationType"/>; each type reads only the ones it needs.</summary>
public class OpenSearchAuthenticationDetails
{
    /// <summary>The user name of <see cref="OpenSearchAuthenticationType.Password"/> authentication.</summary>
    public string Username { get; set; } = string.Empty;
    /// <summary>The password of <see cref="OpenSearchAuthenticationType.Password"/> authentication.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>An OpenSearch API key (3.x security plugin), sent as <c>Authorization: ApiKey &lt;key&gt;</c>.</summary>
    public string ApiKey { get; set; } = string.Empty;
}
