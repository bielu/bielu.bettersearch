namespace Bielu.BetterSearch.Abstractions.Query;

public interface ICreatableSearchQuery
{
    void Add(Occurrence key, ISearchSubQuery value);
}
