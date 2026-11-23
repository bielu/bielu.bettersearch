namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    /// <summary>Matches the documents whose <see cref="Field"/> holds exactly <see cref="Value"/>.</summary>
    public class TermSubQuery : ISearchSubQuery
    {
        /// <inheritdoc />
        public Occurrence Occurrence { get; set; } = Occurrence.Must;
        /// <inheritdoc />
        public double BoostValue { get; set; } = 1;
        /// <summary>The field to match.</summary>
        public string? Field { get; set; }
        /// <summary>The exact value to match.</summary>
        public object? Value { get; set; }
    }
}
