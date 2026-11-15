namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    public class StringRangeSubQuery : BaseRangeSubQuery
    {
        public string? MinValue { get; set; }
        public string? MaxValue { get; set; }
    }
}
