using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public interface IBaseQueryConfigurator
    {
        public ISearchQuery<ISearchResult<ISearchModel>> Query { get; set; }


        Occurance Occurance { get; set; }
    }
}
