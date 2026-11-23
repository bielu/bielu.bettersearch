using Bielu.BetterSearch.Abstractions.Query.Highlighter;

namespace Bielu.BetterSearch.FluentApi.Fluent.Extensions
{
    /// <summary>Extension methods that add highlight requests to a query configurator.</summary>
    public static class CreateHighlighterQueryHelper
    {
        //todo: better dsl coverage
        /// <summary>Creates a highlight request of type <typeparamref name="T"/>, configures it with <paramref name="query"/> and adds it to <see cref="ISearchQuery.HighlightQueries"/>.</summary>
        /// <typeparam name="T">The highlight request type.</typeparam>
        /// <param name="configurator">The configurator whose query gets the highlight request.</param>
        /// <param name="query">Configures the highlight request.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
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
            Action<HighlighterConfigurator> query)
        {
            var booleanQuery = new HighlighterConfigurator(configurator.Query);
            query.Invoke(booleanQuery);

            return configurator;
        }
    }
}
