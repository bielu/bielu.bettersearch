namespace Bielu.BetterSearch.Abstractions.Query;

public interface INestableQuery : ISearchSubQuery, ICreatableSearchQuery
{
    /// <summary>The nested clauses, each combined by its own <see cref="ISearchSubQuery.Occurrence"/>.</summary>
    IList<ISearchSubQuery> NestedQueries { get; set; }
}
