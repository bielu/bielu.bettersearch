namespace Bielu.BetterSearch.Abstractions.Query;

public interface ICreatableSearchQuery<TKey,TValue>
{
    void Add(TKey key, TValue value);
}
