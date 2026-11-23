using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query.Highlighter;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    /// <summary>Configures a highlight request (<see cref="BaseHighlightQuery"/>).</summary>
    public class HighlighterConfigurator
    {
        /// <summary>Creates a new highlight request and adds it to the <see cref="ISearchQuery.HighlightQueries"/> of <paramref name="searchSubQueries"/>.</summary>
        /// <param name="searchSubQueries">The search query that gets the highlight request.</param>
        public HighlighterConfigurator(ISearchQuery searchSubQueries)
        {
            this.Query = new BaseHighlightQuery();
            searchSubQueries.HighlightQueries.Add(this.Query);
        }

        /// <summary>Highlights <paramref name="fieldName"/>.</summary>
        public HighlighterConfigurator WithField(string fieldName)
        {
            Query.Field = fieldName;
            Query.IsAllField = false;
            return this;
        }

        /// <summary>Highlights every field, rather than one.</summary>
        public HighlighterConfigurator ForAllFields()
        {
            Query.IsAllField = true;
            return this;
        }

        /// <summary>
        /// Highlights where the bool query <paramref name="query"/> builds matches, rather than where the main query does.
        /// Calling it again replaces the earlier query.
        /// </summary>
        public HighlighterConfigurator WithQuery(Action<BooleanQueryConfigurator> query)
        {
            var booleanQuery = new BooleanQueryConfigurator();
            query.Invoke(booleanQuery);
            booleanQuery.Query.Occurrence = Occurrence.Must;
            Query.Queries = [booleanQuery.Query];
            return this;
        }

        /// <summary>The highlight request this configurator builds.</summary>
        public IHighlightQuery Query { get; set; }
    }
}
