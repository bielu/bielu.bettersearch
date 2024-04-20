﻿using Bielu.BetterSearch.Abstractions.Query.Highlighter;

namespace Bielu.BetterSearch.FluentApi.Fluent.Extensions
{
    public static class CreateHighlighterQueryHelper
    {
        //todo: better dsl coverage
        public static IBaseQueryConfigurator CreateHighlighterQuery<T>(this IBaseQueryConfigurator configurator,
            Action<T> query) where T :  IHighlightQuery, new()
        {
            var booleanQuery = new T();
            query.Invoke(booleanQuery);
            configurator.Query.HighlightQueries.Add(booleanQuery);
            return configurator;
        }
        //todo: better dsl coverage
        public static IBaseQueryConfigurator CreateHighlighterQuery<T>(this IBaseQueryConfigurator configurator,
            Action<HighLighterConfigurator> query) where T :  IHighlightQuery, new()
        {
            var booleanQuery = new HighLighterConfigurator(configurator.Query);
            query.Invoke(booleanQuery);

            return configurator;
        }
    }
}
