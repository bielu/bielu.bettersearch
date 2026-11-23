namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    /// <summary>Matches the documents whose <see cref="Field"/> holds exactly any of the values in <see cref="Value"/>.</summary>
    public class TermsSubQuery : ISearchSubQuery
    {
        /// <inheritdoc />
        public Occurrence Occurrence { get; set; } = Occurrence.Must;
        /// <inheritdoc />
        public double BoostValue { get; set; } = 1;
        /// <summary>The field to match.</summary>
        public string? Field { get; set; }
        /// <summary>The values to match.</summary>
        public List<object>? Value { get; set; }
    }
}
