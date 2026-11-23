using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

/// <summary>Translates a query's clauses into the engine's query type.</summary>
/// <typeparam name="T">The engine's query type.</typeparam>
public interface IQueryTranslateServiceAsync<T>
{
    /// <summary>Translates the <c>Query</c> clauses of <paramref name="query"/>.</summary>
    /// <param name="query">The query to translate.</param>
    /// <returns>The engine query, or a failed <see cref="Result"/> when a clause can't be translated.</returns>
    Task<Result<T>> TranslateMainQueryAsync(ISearchQuery query);
}
