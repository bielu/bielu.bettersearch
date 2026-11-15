namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    public class FuzzySubQuery : ISearchSubQuery
    {
        public Occurrence Occurrence { get; set; } = Occurrence.Must;
        public int BoostValue { get; set; } = 1;
        public string? Field { get; set; }
        public object? Value { get; set; }
        public bool Transposition { get; set; }
        public int PrefixLength { get; set; }
        public int MaxExpansions { get; set; }
        /// <summary>
        /// The most edits a match may be from <see cref="Value"/>. Elasticsearch, OpenSearch and PostgreSQL allow at
        /// most 2 and, when <c>null</c>, pick it from the length of the term, as Elasticsearch's <c>AUTO</c>. Lifti uses
        /// the value as given, and 2 for every term when <c>null</c>. See the feature matrix for each provider.
        /// </summary>
        public int? MaxEdits { get; set; }
    }
}
