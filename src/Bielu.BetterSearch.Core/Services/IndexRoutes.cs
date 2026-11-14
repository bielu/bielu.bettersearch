using FluentResults;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// The routes the unkeyed services use, fixed when <c>AddBetterSearch</c> runs: index name (ignoring case) to the
/// registered key, the default key for an index with no route, and every registered key for the error messages.
/// </summary>
internal sealed class IndexRoutes(IReadOnlyDictionary<string, string> routes, string? defaultKey, IReadOnlyList<string> keys)
{
    private readonly Dictionary<string, string> _routes = new(routes, StringComparer.OrdinalIgnoreCase);

    /// <summary>The key of the provider for <paramref name="index"/>: its route, else the default, else a failed <see cref="Result"/>.</summary>
    public Result<string> KeyFor(string? index)
    {
        if (index is not null && _routes.TryGetValue(index, out var key))
        {
            return Result.Ok(key);
        }

        if (defaultKey is not null)
        {
            return Result.Ok(defaultKey);
        }

        var name = index is null ? "with no name" : $"'{index}'";
        return Result.Fail<string>(
            $"No provider is routed for the index {name}, and there is no default provider. " +
            $"Route it with RouteIndex(index, key) or the {Configuration.SearchApplicationConfiguration.RoutesSectionKey} section, or choose a default with SetDefaultProvider. " +
            $"Registered keys: {string.Join(", ", keys.Select(k => $"'{k}'"))}.");
    }
}
