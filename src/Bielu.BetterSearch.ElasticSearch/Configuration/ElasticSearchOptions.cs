using Bielu.BetterSearch.Abstractions.Configuration;

namespace Bielu.BetterSearch.ElasticSearch.Configuration;

public class ElasticSearchOptions
{
    public static string ElasticSearchOptionsKey { get; }= "Bielu:BetterSearch:ElasticSearch";
    public ElasticSearchIndexSettings SharedIndexSettings { get; set; }
    public Dictionary<string, ElasticSearchIndexSettings>? Indexes { get; set; }

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
