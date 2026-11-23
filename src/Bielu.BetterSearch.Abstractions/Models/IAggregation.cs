namespace Bielu.BetterSearch.Abstractions.Models
{
    /// <summary>The result of one of the query's <c>FacetQueries</c>.</summary>
    public interface IAggregation
    {
        /// <summary>The <c>AggregationName</c> of the facet query this is the result of.</summary>
        public string AggregationName { get; }
    }
}
