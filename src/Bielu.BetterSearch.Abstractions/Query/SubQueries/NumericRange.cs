namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    public class NumericRange : BaseRangeQuery
    {
        public int? MinValue { get; set; }
        public int? MaxValue { get; set; }
    }
}