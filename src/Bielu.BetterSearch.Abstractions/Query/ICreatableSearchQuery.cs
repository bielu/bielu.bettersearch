namespace Bielu.BetterSearch.Abstractions.Query;

/// <summary>A query that clauses can be added to, each with its own <see cref="Occurrence"/>.</summary>
public interface ICreatableSearchQuery
{
    /// <summary>Adds <paramref name="value"/> as a clause, setting its <see cref="ISearchSubQuery.Occurrence"/> to <paramref name="key"/>.</summary>
    /// <remarks>The occurrence lives on the sub-query, so one instance added in two places takes the last occurrence in both.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
    void Add(Occurrence key, ISearchSubQuery value);
}
