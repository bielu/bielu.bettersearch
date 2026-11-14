namespace Bielu.BetterSearch.Abstractions.Fields;

/// <summary>
/// One key of a query's <c>SortOrder</c>: hits are sorted by <see cref="FieldName"/>, ascending unless
/// <see cref="Desc"/> is set. Keys sort by <see cref="Order"/>, lowest first; keys with the same <c>Order</c> keep
/// their place in the list. A provider fails the search when <c>FieldName</c> is empty.
/// </summary>
public class SortOrderField : ISortOrderField
{
    public string FieldName { get; set; } = string.Empty;
    public bool Desc { get; set; }
    public int Order { get; set; }
}
