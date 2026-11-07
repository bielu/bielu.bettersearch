using Bielu.BetterSearch.Abstractions.Configuration;

namespace Bielu.BetterSearch.OpenSearch.Configuration;

public class OpenSearchOptions
{
    public static string OpenSearchOptionsKey { get; } = "Bielu:BetterSearch:OpenSearch";
    public OpenSearchIndexSettings SharedIndexSettings { get; set; } = new();
    public Dictionary<string, OpenSearchIndexSettings>? Indexes { get; set; }

    /// <summary>
    /// The deepest result a search may page to, <c>(Page - 1) * PageSize + PageSize</c>. Match it to the
    /// indexes' <c>index.max_result_window</c>, which is 10,000 unless changed.
    /// </summary>
    public int MaxResultWindow { get; set; } = 10_000;
}

public class OpenSearchIndexSettings : IndexSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public OpenSearchAuthenticationType AuthenticationType { get; set; }
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
    public string ApiKey { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
}
