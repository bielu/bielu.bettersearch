using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;

namespace Bielu.BetterSearch.ElasticSearch.Services;

/// <summary>
/// Maps hits to a <see cref="SearchHit"/> with their score, holding a <see cref="BaseSearchModel"/>: the
/// document id unchanged, its type as <c>ContentType</c> and its fields as <c>CustomProperties</c>,
/// a field stored as <c>null</c> becoming an empty list and <c>null</c> values left out.
/// </summary>
public class ElasticSearchResultMapper : IResultMapper<SearchResponse<SearchDocument>>
{
    public ISearchResult MapResult(SearchResponse<SearchDocument> searchEngineResult) => new BaseSearchResults
    {
        Items = searchEngineResult.Hits.Select(MapHit).ToList(),
        Pagination = new Pagination { TotalCount = searchEngineResult.Total }
    };

    private static ISearchHit MapHit(Hit<SearchDocument> hit) => new SearchHit(new BaseSearchModel
    {
        Id = hit.Id ?? hit.Source?.Id ?? string.Empty,
        ContentType = hit.Source?.Type ?? string.Empty,
        CustomProperties = StoredFields.ToCustomProperties(hit.Source?.Fields)
    }, hit.Score);
}
