using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Query;
using FluentResults;

namespace Bielu.BetterSearch.Services;

/// <summary>Reads a query's <c>SortOrder</c> the same way for every provider.</summary>
internal static class SortOrders
{
    public const string NullEntry = "SortOrder holds a null entry.";

    public const string MissingFieldName = "A SortOrder entry needs a FieldName.";

    /// <summary>
    /// The entries of <paramref name="query"/>'s <c>SortOrder</c> by <c>Order</c>, keeping the list's order between
    /// entries with the same <c>Order</c>; empty when it has none. Fails on a <c>null</c> entry and on an entry with
    /// a <c>null</c>, empty or blank <c>FieldName</c>.
    /// </summary>
    public static Result<IReadOnlyList<ISortOrderField>> Ordered(ISearchQuery query)
    {
        foreach (var sort in query.SortOrder ?? [])
        {
            if (sort == null)
            {
                return Result.Fail<IReadOnlyList<ISortOrderField>>(NullEntry);
            }

            if (string.IsNullOrWhiteSpace(sort.FieldName))
            {
                return Result.Fail<IReadOnlyList<ISortOrderField>>(MissingFieldName);
            }
        }

        return Result.Ok<IReadOnlyList<ISortOrderField>>((query.SortOrder ?? []).OrderBy(sort => sort.Order).ToList());
    }
}
