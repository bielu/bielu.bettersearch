using Bielu.BetterSearch.Abstractions.Configuration;

namespace Bielu.BetterSearch.ElasticSearch.Configuration;

public class ElasticSearchOptions
{
    public ElasticSearchIndexSettings SharedIndexSettings { get; set; }
    public Dictionary<string, ElasticSearchIndexSettings> Indexes { get; set; }

}

public class ElasticSearchIndexSettings : IndexSettings
{
    public bool CustomConnectionString { get; set; }
}
