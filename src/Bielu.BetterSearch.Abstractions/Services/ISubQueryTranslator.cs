using Bielu.BetterSearch.Abstractions.Query;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

public interface ISubQueryTranslator<T,TQuery> where T : class, ISearchSubQuery
{
    bool CanTranslate(ISearchSubQuery query);
    Task<Result<TQuery>> TranslateAsync(ISearchSubQuery query);
}
