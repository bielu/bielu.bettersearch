namespace Bielu.BetterSearch.Abstractions.Query;

public interface ICreatableSearchQuery
{
    /// <summary>Adds <paramref name="value"/> as a clause, setting its <see cref="ISearchSubQuery.Occurrence"/> to <paramref name="key"/>.</summary>
    /// <remarks>The occurrence lives on the sub-query, so one instance added in two places takes the last occurrence in both.</remarks>
    void Add(Occurrence key, ISearchSubQuery value);
}
