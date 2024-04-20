namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    public class DateRangeQuery : BaseRangeQuery
    {
        public DateTime? MinValue { get; set; }
        public DateTime? MaxValue { get; set; }
    }
}