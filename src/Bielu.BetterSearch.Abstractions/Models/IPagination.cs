namespace Bielu.BetterSearch.Abstractions.Models;

/// <summary>Where a page of results sits among all the matches.</summary>
public interface IPagination
{
    /// <summary>The number of documents the query matched, across every page.</summary>
    public long TotalCount { get; }
    /// <summary>The page the result holds, counting from 1.</summary>
    public int Page { get; }
    /// <summary>The number of hits per page.</summary>
    public int PageSize { get; }
    /// <summary>The number of pages the matches fill at <see cref="PageSize"/>.</summary>
    public int TotalPages { get; }
}
