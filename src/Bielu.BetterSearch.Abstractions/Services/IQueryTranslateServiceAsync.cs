using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

public interface IQueryTranslateServiceAsync<T>
{
    Task<Result<T>> TranslateMainQuery(ISearchQuery<ISearchResult<ISearchModel>> query);
}
