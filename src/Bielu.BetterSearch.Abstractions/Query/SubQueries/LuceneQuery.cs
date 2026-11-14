namespace Bielu.BetterSearch.Abstractions.Query.SubQueries;

public class LuceneSubQuery : ISearchSubQuery
{
    public Occurance Occurance { get; set; } = Occurance.MUST;
    public int BoostValue { get; set; } = 1;
    public string? LuceneQuery { get; set; }
}
