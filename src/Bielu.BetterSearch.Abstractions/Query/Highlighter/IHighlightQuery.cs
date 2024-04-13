namespace Bielu.BetterSearch.Abstractions.Query.Highlighter;

public interface IHighlightQuery
{
    string Field { get; set; }
    bool IsAllField { get; set; }
    Dictionary<Occurance, ISearchSubQuery> Queries { get; set; }
}
