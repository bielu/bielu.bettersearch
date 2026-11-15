
using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class FluentQueryConfigurator(ISearchQuery baseQuery) : IBaseQueryConfigurator
    {
        public ISearchQuery Query { get; set; } = baseQuery;
        public Occurrence Occurrence { get; set; }
    }
}
