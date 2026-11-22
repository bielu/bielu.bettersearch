namespace Bielu.BetterSearch.Abstractions.Query.Highlighter
{
    public class BaseHighlightQuery : IHighlightQuery
    {
        public string Field { get; set; }
        public bool IsAllField { get; set; }

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
