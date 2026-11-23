
using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.FluentApi;

/// <summary>Builds an <see cref="ISearchQuery"/> through the fluent API.</summary>
public interface IFluentApiSearchQueryCreator
{
    /// <summary>Adds the sub-queries, facets and highlight requests that <paramref name="configurator"/> creates to the search query, with <c>Must</c> as the default occurrence.</summary>
    /// <param name="configurator">Configures the search query.</param>
    /// <returns>The search query.</returns>
    ISearchQuery CreateSearchQuery(Action<IBaseQueryConfigurator> configurator);
}
