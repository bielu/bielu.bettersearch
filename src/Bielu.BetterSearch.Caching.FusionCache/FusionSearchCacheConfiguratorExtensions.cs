using Bielu.BetterSearch.Abstractions.Caching;
using Bielu.BetterSearch.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Bielu.BetterSearch.Caching.FusionCache;

public static class FusionSearchCacheConfiguratorExtensions
{
    /// <summary>
    /// Caches search results in FusionCache, through <see cref="FusionSearchResultCache"/>. Register FusionCache itself
    /// with <c>AddFusionCache()</c>; <paramref name="cacheName"/> picks a named cache, and <c>null</c> the default one.
    /// With several instances, give FusionCache a distributed cache and a backplane, so evictions reach every node.
    /// </summary>
    public static Configurator AddFusionSearchCache(this Configurator configurator, string? cacheName = null,
        Action<SearchCacheOptions>? configure = null)
    {
        configurator.Services.TryAddSingleton<ISearchResultCache>(provider => new FusionSearchResultCache(
            cacheName is null
                ? provider.GetRequiredService<IFusionCache>()
                : provider.GetRequiredService<IFusionCacheProvider>().GetCache(cacheName)));
        return configurator.ConfigureSearchCache(configure);
    }
}
