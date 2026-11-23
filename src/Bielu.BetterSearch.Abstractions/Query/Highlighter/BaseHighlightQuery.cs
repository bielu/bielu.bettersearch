namespace Bielu.BetterSearch.Abstractions.Query.Highlighter
{
    /// <summary>A request to highlight one field, or every field, of each hit.</summary>
    public class BaseHighlightQuery : IHighlightQuery
    {
        /// <inheritdoc />
        public string? Field { get; set; }
        /// <inheritdoc />
        public bool IsAllField { get; set; }

        /// <inheritdoc />
        public IList<ISearchSubQuery> Queries { get; set; } = [];

        /// <inheritdoc />
        public void Add(Occurrence key, ISearchSubQuery value)
        {
            ArgumentNullException.ThrowIfNull(value);
            value.Occurrence = key;
            Queries.Add(value);
        }
    }
}
