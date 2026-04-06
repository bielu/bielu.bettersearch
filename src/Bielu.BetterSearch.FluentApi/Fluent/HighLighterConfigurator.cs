using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query.Highlighter;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class HighLighterConfigurator
    {
        public HighLighterConfigurator(ISearchQuery<ISearchResult<ISearchModel>> searchSubQueries)
        {
            searchSubQueries  .HighlightQueries.Add(this.Query);
            this.Query = new BaseHighlightQuery();
        }

        public IHighlightQuery Query { get; set; }
    }
}
