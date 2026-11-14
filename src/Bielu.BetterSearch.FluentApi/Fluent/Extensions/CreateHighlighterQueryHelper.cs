using Bielu.BetterSearch.Abstractions.Query.Highlighter;

namespace Bielu.BetterSearch.FluentApi.Fluent.Extensions
{
    public static class CreateHighlighterQueryHelper
    {
        //todo: better dsl coverage
        public static IBaseQueryConfigurator CreateHighlighterQuery<T>(this IBaseQueryConfigurator configurator,
            Action<T> query) where T : IHighlightQuery, new()
        {
            var booleanQuery = new T();
            query.Invoke(booleanQuery);
            configurator.Query.HighlightQueries.Add(booleanQuery);
            return configurator;
        }
        /// <summary>Adds a highlight request that <paramref name="query"/> configures.</summary>
        public static IBaseQueryConfigurator CreateHighlighterQuery(this IBaseQueryConfigurator configurator,
            Action<HighLighterConfigurator> query)
        {
            var booleanQuery = new HighLighterConfigurator(configurator.Query);
            query.Invoke(booleanQuery);

            return configurator;
        }
    }
}
