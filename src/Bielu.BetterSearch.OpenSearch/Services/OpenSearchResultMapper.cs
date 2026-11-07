using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Services;
using OpenSearch.Client;

namespace Bielu.BetterSearch.OpenSearch.Services;

/// <summary>
/// Maps hits to <see cref="BaseSearchModel"/>: the document id (unchanged in <c>Key</c>, as
/// <c>Id</c> lower-cases it), its score, its type as
/// <c>ContentType</c> and its fields as <c>CustomProperties</c>.
/// </summary>
internal sealed class OpenSearchResultMapper : IResultMapper<ISearchResponse<SearchDocument>>
{
    public ISearchResult<ISearchModel> MapResult(ISearchResponse<SearchDocument> searchEngineResult) => new BaseSearchResults
    {
        Items = searchEngineResult.Hits.Select(MapHit).ToList(),
        Pagination = new Pagination { TotalCount = searchEngineResult.Total }
    };

    private static ISearchModel MapHit(IHit<SearchDocument> hit) => new BaseSearchModel
    {
        Id = hit.Id ?? hit.Source?.Id ?? string.Empty,
        Key = hit.Id ?? hit.Source?.Id ?? string.Empty,
        Score = hit.Score,
        ContentType = hit.Source?.Type ?? string.Empty,
        CustomProperties = hit.Source?.Fields.ToDictionary(
            field => field.Key,
            field => field.Value.ToList()) ?? []
    };
}
