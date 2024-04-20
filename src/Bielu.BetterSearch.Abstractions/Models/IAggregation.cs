namespace Bielu.BetterSearch.Abstractions.Models
{
    public interface IAggregation
    {
        public string AggregationName { get; set; }
        IEnumerable<IAggregation> NestedAggregation { get; set; }
    }
}