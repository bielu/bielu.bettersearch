namespace Bielu.BetterSearch.Abstractions.Fields;

/// <summary>One key of a query's <c>SortOrder</c>; see <see cref="SortOrderField"/>.</summary>
public interface ISortOrderField
{
    /// <summary>The field the hits are sorted by.</summary>
    string FieldName { get; set; }
    /// <summary>Whether the key sorts descending instead of ascending.</summary>
    bool Desc { get; set; }
    /// <summary>The key's place among the query's sort keys, lowest first.</summary>
    int Order { get; set; }
}
