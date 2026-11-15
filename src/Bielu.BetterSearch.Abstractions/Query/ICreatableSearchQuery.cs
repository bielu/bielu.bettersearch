namespace Bielu.BetterSearch.Abstractions.Query;

public interface ICreatableSearchQuery
{
    void Add(Occurance key, ISearchSubQuery value);
}
