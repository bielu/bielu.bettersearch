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

        public IHighlightQuery Query { get; set; }
    }
}
