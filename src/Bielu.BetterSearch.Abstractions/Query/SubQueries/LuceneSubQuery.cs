namespace Bielu.BetterSearch.Abstractions.Query.SubQueries;

/// <summary>A clause written in raw Lucene query syntax.</summary>
public class LuceneSubQuery : ISearchSubQuery
{
    /// <inheritdoc />
    public Occurrence Occurrence { get; set; } = Occurrence.Must;
    /// <inheritdoc />
    public double BoostValue { get; set; } = 1;
    /// <summary>The Lucene query text.</summary>
    public string? LuceneQuery { get; set; }
}
