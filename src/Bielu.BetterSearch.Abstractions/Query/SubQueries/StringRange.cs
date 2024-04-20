namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    public class StringRange : BaseRangeQuery
    {
        public string? MinValue { get; set; }
        public string? MaxValue { get; set; }
    }
}