
using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    /// <summary>The top-level <see cref="IBaseQueryConfigurator"/>, over a search query.</summary>
    /// <param name="baseQuery">The search query to configure.</param>
    public class FluentQueryConfigurator(ISearchQuery baseQuery) : IBaseQueryConfigurator
    {
        /// <inheritdoc />
        public ISearchQuery Query { get; set; } = baseQuery;
        /// <inheritdoc />
        public Occurrence Occurrence { get; set; }
    }
}
