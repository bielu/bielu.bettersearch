namespace Bielu.BetterSearch.Abstractions.Query
{
    public interface INestableQuery : ISearchSubQuery, ICreatableSearchQuery
    {

        List<ISearchSubQuery> NestedQueries { get; set; }
    }
}
