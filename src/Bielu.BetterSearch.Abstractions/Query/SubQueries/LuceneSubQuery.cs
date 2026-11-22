namespace Bielu.BetterSearch.Abstractions.Query.SubQueries;

public class LuceneSubQuery : ISearchSubQuery
{
    public Occurrence Occurrence { get; set; } = Occurrence.Must;
    public double BoostValue { get; set; } = 1;
    public string? LuceneQuery { get; set; }
}
