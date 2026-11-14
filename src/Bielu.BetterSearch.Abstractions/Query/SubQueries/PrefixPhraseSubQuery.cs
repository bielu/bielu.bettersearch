namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    public class PrefixPhraseSubQuery : ISearchSubQuery
    {
        public Occurance Occurance { get; set; } = Occurance.MUST;
        public int BoostValue { get; set; } = 1;
        public string? Field { get; set; }
        public object? Value { get; set; }

    }
}
