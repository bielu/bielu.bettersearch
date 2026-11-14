using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query.Highlighter;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class HighLighterConfigurator
    {
        public HighLighterConfigurator(ISearchQuery searchSubQueries)
        {
            this.Query = new BaseHighlightQuery();
            searchSubQueries.HighlightQueries.Add(this.Query);
        }

        /// <summary>Highlights <paramref name="fieldName"/>.</summary>
        public HighLighterConfigurator WithField(string fieldName)
        {
            Query.Field = fieldName;
            Query.IsAllField = false;
            return this;
        }

        /// <summary>Highlights every field, rather than one.</summary>
        public HighLighterConfigurator ForAllFields()
        {
            Query.IsAllField = true;
            return this;
        }

        /// <summary>
        /// Highlights where the bool query <paramref name="query"/> builds matches, rather than where the main query does.
        /// Calling it again replaces the earlier query.
        /// </summary>
        public HighLighterConfigurator WithQuery(Action<BooleanQueryConfigurator> query)
        {
            var booleanQuery = new BooleanQueryConfigurator();
            query.Invoke(booleanQuery);
            Query.Queries = new Dictionary<Occurance, ISearchSubQuery> { [Occurance.MUST] = booleanQuery.Query };
            return this;
        }

        public IHighlightQuery Query { get; set; }
    }
}
