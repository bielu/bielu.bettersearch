namespace Bielu.BetterSearch.Abstractions.Query
{
    public class SearchSubQuery(List<ISearchSubQuery> nestedQueries) : ISearchSubQuery
    {
        public Occurance Occurance { get; set; } = Occurance.Must;
        public string Field { get; set; }
        public int BoostValue { get; set; } = 1;
        public List<ISearchSubQuery> NestedQueries { get; set; } = nestedQueries;
    }
}
