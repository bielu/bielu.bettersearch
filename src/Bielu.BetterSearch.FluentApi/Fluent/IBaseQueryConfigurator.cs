using SImpl.SearchModule.Abstraction.Results;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public interface IBaseQueryConfigurator
    {
        public ISearchQuery<IQueryResult> Query { get; set; }


        Occurance Occurance { get; set; }
    }
}
