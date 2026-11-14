namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    /// <summary>
    /// A range over one field. Both edges are inclusive by default, so a range from 1 to 10 matches 1 and 10. Set
    /// a minimum, a maximum or both: a range with neither fails to translate rather than matching every
    /// document that has the field.
    /// </summary>
    public class BaseRangeQuery : ISearchSubQuery
    {
        public Occurance Occurance { get; set; } = Occurance.MUST;
        public int BoostValue { get; set; } = 1;
        public string? Field { get; set; }
        /// <summary>Whether the minimum itself matches. <c>true</c> by default.</summary>
        public bool IncludeMinEdge { get; set; } = true;
        /// <summary>Whether the maximum itself matches. <c>true</c> by default.</summary>
        public bool IncludeMaxEdge { get; set; } = true;
    }
}
