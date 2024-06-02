namespace Bielu.BetterSearch.Abstractions.Query.SubQueries;

public class LuceneSubQuery : ISearchSubQuery
{
    public Occurance Occurance { get; set; }
    public int BoostValue { get; set; }
    public string? LuceneQuery { get; set; }
}
