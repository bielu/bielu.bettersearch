namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    public class BoolSearchSubQuery : ISearchSubQuery, INestableQuery
    {
        public Occurrence Occurrence { get; set; } = Occurrence.Must;
        public string? Field { get; set; }
        public double BoostValue { get; set; } = 1;
        public IList<ISearchSubQuery> NestedQueries { get; set; } = [];

        /// <inheritdoc />
        public void Add(Occurrence key, ISearchSubQuery value)
        {
            ArgumentNullException.ThrowIfNull(value);
            value.Occurrence = key;
            NestedQueries.Add(value);
        }
    }
}
