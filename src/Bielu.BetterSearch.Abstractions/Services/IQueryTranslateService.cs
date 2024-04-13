namespace Bielu.BetterSearch.Abstractions.Services;

public interface IQueryTranslateService<T>
{
    T TranslateQuery(string query);
}
