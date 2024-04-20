namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    public class TermsSubQuery : ISearchSubQuery
    {
        public Occurance Occurance { get; set; }
        public int BoostValue { get; set; } = 1;
        public string? Field { get; set; }
        public List<object>? Value { get; set; }
    }
}
