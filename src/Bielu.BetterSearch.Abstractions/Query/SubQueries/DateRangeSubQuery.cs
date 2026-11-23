namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    /// <summary>
    /// A range over dates. Each bound is a <see cref="DateRangeValue"/>, which a <see cref="DateTime"/> or a
    /// <see cref="DateTimeOffset"/> converts to implicitly; a <see cref="DateTime"/> with
    /// <see cref="DateTimeKind.Unspecified"/> is read as UTC on every provider.
    /// </summary>
    public class DateRangeSubQuery : BaseRangeSubQuery
    {
        /// <summary>The earliest date that matches, or <c>null</c> for no lower bound.</summary>
        public DateRangeValue? MinValue { get; set; }
        /// <summary>The latest date that matches, or <c>null</c> for no upper bound.</summary>
        public DateRangeValue? MaxValue { get; set; }
    }
}
