namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    public class TermsSubQuery : ISearchSubQuery
    {
        public Occurrence Occurrence { get; set; } = Occurrence.Must;
        public double BoostValue { get; set; } = 1;
        public string? Field { get; set; }
        public List<object>? Value { get; set; }
    }
}
