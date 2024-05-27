namespace Bielu.BetterSearch.Abstractions.Services;

public interface IQueryTranslateServiceAsync<T>
{
    Task<T> TranslateQuery(string query);
}
