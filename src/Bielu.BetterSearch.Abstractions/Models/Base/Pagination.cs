namespace Bielu.BetterSearch.Abstractions.Models
{
    /// <summary>Where a page of results sits among all the matches.</summary>
    public class Pagination : IPagination
    {
        /// <inheritdoc />
        public long TotalCount { get; init; }
        /// <inheritdoc />
        public int Page { get; init; }
        /// <inheritdoc />
        public int PageSize { get; init; }
        /// <inheritdoc />
        public int TotalPages { get; init; }
    }
}
