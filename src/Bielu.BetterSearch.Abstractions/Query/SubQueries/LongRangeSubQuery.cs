namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    public class LongRangeSubQuery : BaseRangeSubQuery
    {
        public long? MinValue { get; set; }
        public long? MaxValue { get; set; }
    }
}
