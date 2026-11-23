namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    /// <summary>Matches the terms of <see cref="Field"/> that start with <see cref="Value"/>.</summary>
    public class PrefixSubQuery : ISearchSubQuery
    {
        /// <inheritdoc />
        public Occurrence Occurrence { get; set; } = Occurrence.Must;
        /// <inheritdoc />
        public double BoostValue { get; set; } = 1;
        /// <summary>The field to match.</summary>
        public string? Field { get; set; }
        /// <summary>The prefix to match.</summary>
        public object? Value { get; set; }

    }
}
