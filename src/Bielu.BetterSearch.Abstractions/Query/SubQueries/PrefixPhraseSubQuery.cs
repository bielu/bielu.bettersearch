namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    /// <summary>Matches <see cref="Value"/> as a phrase in <see cref="Field"/>, its last word as a prefix.</summary>
    public class PrefixPhraseSubQuery : ISearchSubQuery
    {
        /// <inheritdoc />
        public Occurrence Occurrence { get; set; } = Occurrence.Must;
        /// <inheritdoc />
        public double BoostValue { get; set; } = 1;
        /// <summary>The field to match.</summary>
        public string? Field { get; set; }
        /// <summary>The phrase to match.</summary>
        public object? Value { get; set; }

    }
}
