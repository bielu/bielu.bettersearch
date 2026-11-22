namespace Bielu.BetterSearch.Abstractions.Models
{
    public class Pagination : IPagination
    {
        public long TotalCount { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalPages { get; init; }
    }
}
