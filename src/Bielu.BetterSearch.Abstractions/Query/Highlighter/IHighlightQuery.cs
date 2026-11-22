namespace Bielu.BetterSearch.Abstractions.Query.Highlighter;

public interface IHighlightQuery : ICreatableSearchQuery
{
    string Field { get; set; }
    bool IsAllField { get; set; }

    /// <summary>The highlight query's clauses, each combined by its own <see cref="ISearchSubQuery.Occurrence"/>.</summary>
    IList<ISearchSubQuery> Queries { get; set; }
}
