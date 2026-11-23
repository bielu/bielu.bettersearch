using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    /// <summary>Configures a top-level search query: its sub-queries, facets and highlight requests.</summary>
    public interface IBaseQueryConfigurator
    {
        /// <summary>The search query that sub-queries, facets and highlight requests are added to.</summary>
        public ISearchQuery Query { get; set; }


        /// <summary>The occurrence that sub-queries created on this configurator are added with when they do not choose their own.</summary>
        Occurrence Occurrence { get; set; }
    }
}
