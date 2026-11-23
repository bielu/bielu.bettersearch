namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    /// <summary>Matches the terms of <see cref="Field"/> within a few edits of <see cref="Value"/>.</summary>
    public class FuzzySubQuery : ISearchSubQuery
    {
        /// <inheritdoc />
        public Occurrence Occurrence { get; set; } = Occurrence.Must;
        /// <inheritdoc />
        public double BoostValue { get; set; } = 1;
        /// <summary>The field to match.</summary>
        public string? Field { get; set; }
        /// <summary>The term to match, a string or a number.</summary>
        public object? Value { get; set; }
        /// <summary>Whether swapping two adjacent characters counts as one edit rather than two.</summary>
        public bool Transposition { get; set; }
        /// <summary>The number of leading characters that must match exactly; 0, the default, leaves it to the engine.</summary>
        public int PrefixLength { get; set; }
        /// <summary>The most terms the query expands to; 0, the default, leaves it to the engine.</summary>
        public int MaxExpansions { get; set; }
        /// <summary>
        /// The most edits a match may be from <see cref="Value"/>. Elasticsearch, OpenSearch and PostgreSQL allow at
        /// most 2 and, when <c>null</c>, pick it from the length of the term, as Elasticsearch's <c>AUTO</c>. Lifti uses
        /// the value as given, and 2 for every term when <c>null</c>. See the feature matrix for each provider.
        /// </summary>
        public int? MaxEdits { get; set; }
    }
}
