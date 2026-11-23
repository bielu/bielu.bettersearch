namespace Bielu.BetterSearch.Abstractions.Query;

/// <summary>A sub-query that holds other sub-queries as its clauses.</summary>
public interface INestableQuery : ISearchSubQuery, ICreatableSearchQuery
{
    /// <summary>The nested clauses, each combined by its own <see cref="ISearchSubQuery.Occurrence"/>.</summary>
    IList<ISearchSubQuery> NestedQueries { get; set; }
}
