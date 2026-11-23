namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    /// <summary>A range over whole numbers, exact above 2^53.</summary>
    public class LongRangeSubQuery : BaseRangeSubQuery
    {
        /// <summary>The lowest value that matches, or <c>null</c> for no lower bound.</summary>
        public long? MinValue { get; set; }
        /// <summary>The highest value that matches, or <c>null</c> for no upper bound.</summary>
        public long? MaxValue { get; set; }
    }
}
