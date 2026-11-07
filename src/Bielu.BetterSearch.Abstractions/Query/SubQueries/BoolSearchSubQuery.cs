namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    public class BoolSearchSubQuery : ISearchSubQuery, INestableQuery
    {
        public Occurance Occurance { get; set; } = Occurance.MUST;
        public string? Field { get; set; }
        public int BoostValue { get; set; } = 1;
        public List<ISearchSubQuery> NestedQueries { get; set; } = [];
        /// <summary>Adds <paramref name="value"/> as a nested clause.</summary>
        /// <remarks>Sets <c>value.Occurance</c> to <paramref name="key"/>, so one instance added in two places takes the last occurrence in both.</remarks>
        public void Add(Occurance key, ISearchSubQuery value)
        {
            value.Occurance = key;
            NestedQueries.Add(value);
        }
    }
}
