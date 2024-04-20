namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    public class BoolSearchSubQuery : INestableQuery
    {
        public Occurance Occurance { get; set; } = Occurance.Must;
        public string? Field { get; set; }
        public int BoostValue { get; set; } = 1;
        public List<ISearchSubQuery> NestedQueries { get; set; } = [];
        public void Add(Occurance key, ISearchSubQuery value) => NestedQueries.Add(value);
    }
}
