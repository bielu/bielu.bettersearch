namespace Bielu.BetterSearch.Abstractions.Models;

public interface IPagination
{
    public long TotalCount { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int TotalPages { get; }
}
