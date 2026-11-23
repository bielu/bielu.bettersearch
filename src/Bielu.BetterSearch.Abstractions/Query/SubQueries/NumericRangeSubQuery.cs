namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    /// <summary>
    /// A range over numbers, fractional ones included (<c>9.99</c> to <c>49.5</c>). An <c>int</c> bound converts
    /// implicitly. Use <see cref="LongRangeSubQuery"/> for whole numbers that need precision above 2^53.
    /// </summary>
    public class NumericRangeSubQuery : BaseRangeSubQuery
    {
        /// <summary>The lowest value that matches, or <c>null</c> for no lower bound.</summary>
        public double? MinValue { get; set; }
        /// <summary>The highest value that matches, or <c>null</c> for no upper bound.</summary>
        public double? MaxValue { get; set; }
    }
}
