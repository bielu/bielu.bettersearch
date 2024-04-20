namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    public class LongRange : BaseRangeQuery
    {
        public long? MinValue { get; set; }
        public long? MaxValue { get; set; }
    }
}