namespace Bielu.BetterSearch.Abstractions.Query.Highlighter;

/// <summary>A request to highlight one field, or every field, of each hit, returned in <see cref="Models.ISearchHit.Highlights"/>.</summary>
public interface IHighlightQuery : ICreatableSearchQuery
{
    /// <summary>The field to highlight. Required unless <see cref="IsAllField"/> is set.</summary>
    string? Field { get; set; }
    /// <summary>Whether to highlight every field instead of <see cref="Field"/>.</summary>
    bool IsAllField { get; set; }

    /// <summary>The highlight query's clauses, each combined by its own <see cref="ISearchSubQuery.Occurrence"/>.</summary>
    IList<ISearchSubQuery> Queries { get; set; }
}
