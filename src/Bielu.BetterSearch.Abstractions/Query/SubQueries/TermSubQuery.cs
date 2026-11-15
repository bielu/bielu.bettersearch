namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    public class TermSubQuery : ISearchSubQuery
    {
        public Occurrence Occurrence { get; set; } = Occurrence.Must;
        public int BoostValue { get; set; } = 1;
        public string? Field { get; set; }
        public object? Value { get; set; }
    }
}
