using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

public interface IPostFilterTranslateServiceAsync<T>
{
    /// <summary>Translates the post filters, or returns <c>null</c> when there are none.</summary>
    Task<Result<T?>> TranslatePostFilterAsync(ISearchQuery<ISearchResult<ISearchModel>> query);
}
