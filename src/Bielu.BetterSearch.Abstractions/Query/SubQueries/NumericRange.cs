namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    /// <summary>
    /// A range over numbers, fractional ones included (<c>9.99</c> to <c>49.5</c>). An <c>int</c> bound converts
    /// implicitly. Use <see cref="LongRange"/> for whole numbers that need precision above 2^53.
    /// </summary>
    public class NumericRange : BaseRangeQuery
    {
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
    }
}
