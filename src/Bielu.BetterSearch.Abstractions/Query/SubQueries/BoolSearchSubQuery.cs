namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    public class BoolSearchSubQuery : ISearchSubQuery, INestableQuery
    {
        public Occurrence Occurrence { get; set; } = Occurrence.Must;
        public string? Field { get; set; }
        public int BoostValue { get; set; } = 1;
        public List<ISearchSubQuery> NestedQueries { get; set; } = [];
        /// <summary>Adds <paramref name="value"/> as a nested clause.</summary>
        /// <remarks>Sets <c>value.Occurrence</c> to <paramref name="key"/>, so one instance added in two places takes the last occurrence in both.</remarks>
        public void Add(Occurrence key, ISearchSubQuery value)
        {
            value.Occurrence = key;
            NestedQueries.Add(value);
        }
    }
}
