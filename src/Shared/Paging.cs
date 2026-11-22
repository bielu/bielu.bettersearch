using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;

namespace Bielu.BetterSearch.Services;

/// <summary>The paging rules every provider shares: the page a query asks for, its size, and the result's <see cref="Pagination"/>.</summary>
internal static class Paging
{
    /// <summary><c>Page</c> counts from 1; 0, the default, is the first page.</summary>
    public static int PageOf(ISearchQuery query) => Math.Max(query.Page, 1);

    /// <summary><c>PageSize</c>, or <paramref name="defaultPageSize"/> when it is 0 or less.</summary>
    public static int PageSizeOf(ISearchQuery query, int defaultPageSize) =>
        query.PageSize > 0 ? query.PageSize : defaultPageSize;

    /// <summary>The pagination of the page <paramref name="query"/> asks for, out of <paramref name="total"/> matches.</summary>
    public static Pagination Of(long total, ISearchQuery query, int defaultPageSize)
    {
        var pageSize = PageSizeOf(query, defaultPageSize);
        return new Pagination
        {
            TotalCount = total,
            Page = PageOf(query),
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };
    }
}
