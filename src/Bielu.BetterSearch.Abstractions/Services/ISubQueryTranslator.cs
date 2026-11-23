using Bielu.BetterSearch.Abstractions.Query;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;


/// <summary>Translates one kind of sub-query into the engine's query type.</summary>
/// <typeparam name="TQuery">The engine's query type.</typeparam>
public interface ISubQueryTranslator<TQuery>
{
    /// <summary>Whether this translator handles <paramref name="query"/>.</summary>
    /// <param name="query">The sub-query.</param>
    bool CanTranslate(ISearchSubQuery query);
    /// <summary>Translates <paramref name="query"/>.</summary>
    /// <param name="query">The sub-query to translate.</param>
    /// <param name="subQueryTranslators">Every registered translator, for translating nested sub-queries.</param>
    /// <returns>The engine query, or a failed <see cref="Result"/> when it can't be translated.</returns>
    Task<Result<TQuery>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<TQuery>> subQueryTranslators);
}
