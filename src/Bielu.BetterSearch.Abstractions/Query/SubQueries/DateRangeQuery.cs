namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    /// <summary>
    /// A range over dates. Each bound is a <see cref="DateRangeValue"/>, which a <see cref="DateTime"/> or a
    /// <see cref="DateTimeOffset"/> converts to implicitly; a <see cref="DateTime"/> with
    /// <see cref="DateTimeKind.Unspecified"/> is read as UTC on every provider.
    /// </summary>
    public class DateRangeQuery : BaseRangeQuery
    {
        public DateRangeValue? MinValue { get; set; }
        public DateRangeValue? MaxValue { get; set; }
    }
}
