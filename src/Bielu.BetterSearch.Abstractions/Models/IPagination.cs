namespace Bielu.BetterSearch.Abstractions.Models;

public interface IPagination
{
    public long TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    int? TotalNumberOfPages { get; set; }
    public string SiteId { get; set; }
    public string BaseUrl { get; set; }
    public string QueryUrl { get; set; }
    public int ListingId { get; set; }
}
