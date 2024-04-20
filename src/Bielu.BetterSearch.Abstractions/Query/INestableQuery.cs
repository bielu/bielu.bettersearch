namespace Bielu.BetterSearch.Abstractions.Query
{
    public interface INestableQuery : ISearchSubQuery,ICreatableSearchQuery<Occurance, ISearchSubQuery>
    {

        List<ISearchSubQuery> NestedQueries { get; set; }
    }
}
