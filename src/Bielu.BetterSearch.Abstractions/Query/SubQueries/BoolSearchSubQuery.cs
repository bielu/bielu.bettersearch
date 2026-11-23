namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    /// <summary>A clause that combines its <see cref="NestedQueries"/>, each by its own occurrence, as a <c>bool</c> query does.</summary>
    public class BoolSearchSubQuery : ISearchSubQuery, INestableQuery
    {
        /// <inheritdoc />
        public Occurrence Occurrence { get; set; } = Occurrence.Must;
        /// <summary>Not used: no provider reads it when translating the bool.</summary>
        public string? Field { get; set; }
        /// <inheritdoc />
        public double BoostValue { get; set; } = 1;
        /// <inheritdoc />
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
