namespace Bielu.BetterSearch.Abstractions.Models
{
    public class Pagination : IPagination
    {
        public long TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
