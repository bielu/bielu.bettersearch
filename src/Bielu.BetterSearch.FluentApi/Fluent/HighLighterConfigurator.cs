using Bielu.BetterSearch.Abstractions.Query.Highlighter;
using Bielu.BetterSearch.Abstractions.Results;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class HighLighterConfigurator
    {
        public HighLighterConfigurator(ISearchQuery<IQueryResult> searchSubQueries)
        {
            searchSubQueries  .HighlightQueries.Add(this.Query);
            this.Query = new BaseHighlightQuery();
        }

        public IHighlightQuery Query { get; set; }
    }
}
