using Bielu.BetterSearch.Abstractions.Configuration;

namespace Bielu.BetterSearch.OpenSearch.Configuration;

public class OpenSearchOptions
{
    public static string OpenSearchOptionsKey { get; } = "Bielu:BetterSearch:OpenSearch";
    public OpenSearchIndexSettings SharedIndexSettings { get; set; } = new();
    /// <summary>
    /// Settings for single indexes, by index name. Names match whatever their case, as configuration keys do,
    /// so <c>Indexes:People</c> applies to the index <c>people</c>.
    /// </summary>
    public Dictionary<string, OpenSearchIndexSettings>? Indexes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

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
