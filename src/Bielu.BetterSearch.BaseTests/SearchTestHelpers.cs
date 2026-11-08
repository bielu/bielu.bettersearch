using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

/// <summary>Query building and id reading shared by the search test classes, so they agree on how ids are ordered.</summary>
public static class SearchTestHelpers
{
    /// <summary>A query on <paramref name="index"/> holding the one clause.</summary>
    public static SearchQuery Query(string index, Occurance occurance, ISearchSubQuery subQuery)
    {
        var query = new SearchQuery { Index = index };
        query.Add(occurance, subQuery);
        return query;
    }

    /// <summary>Runs <paramref name="query"/>, checks it succeeds, showing the provider's errors when not, and returns the hit ids in ordinal order.</summary>
    public static async Task<List<string>> SearchIdsAsync(this ISearchServiceAsync searchService, SearchQuery query, CancellationToken cancellationToken = default)
    {
        var result = await searchService.SearchAsync(query, cancellationToken);
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        return result.Value.Items.Select(item => item.Document.Id).Order(StringComparer.Ordinal).ToList();
    }
}
