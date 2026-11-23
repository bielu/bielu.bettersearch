using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

/// <summary>Translates a query's <c>PostFilterQuery</c> into the engine's query type.</summary>
/// <typeparam name="T">The engine's query type.</typeparam>
public interface IPostFilterTranslateServiceAsync<T>
{
    /// <summary>Translates the post filters, or returns <c>null</c> when there are none.</summary>
    Task<Result<T?>> TranslatePostFilterAsync(ISearchQuery query);
}
