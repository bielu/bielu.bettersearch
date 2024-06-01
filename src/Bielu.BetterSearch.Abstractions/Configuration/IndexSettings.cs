namespace Bielu.BetterSearch.Abstractions.Configuration;

public class IndexSettings
{
    public string IndexName { get; set; }
    public string AliasName { get; set; }
    public string TypeName { get; set; }
    public bool AliasingEnabled { get; set; }
    public bool IndexingEnabled { get; set; }
}
