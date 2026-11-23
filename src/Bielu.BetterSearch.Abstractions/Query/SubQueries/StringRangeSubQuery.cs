namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    /// <summary>A range over strings, compared as text.</summary>
    public class StringRangeSubQuery : BaseRangeSubQuery
    {
        /// <summary>The lowest value that matches, or <c>null</c> for no lower bound.</summary>
        public string? MinValue { get; set; }
        /// <summary>The highest value that matches, or <c>null</c> for no upper bound.</summary>
        public string? MaxValue { get; set; }
    }
}
