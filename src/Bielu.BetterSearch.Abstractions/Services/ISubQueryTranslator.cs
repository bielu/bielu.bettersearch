using Bielu.BetterSearch.Abstractions.Query;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

public interface ISubQueryTranslator<T,TQuery> : ISubQueryTranslator<TQuery> where T : class, ISearchSubQuery
{

}

public interface ISubQueryTranslator<TQuery>
{
    bool CanTranslate(ISearchSubQuery query);
    Task<Result<TQuery>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<TQuery>> subQueryTranslators);
}
