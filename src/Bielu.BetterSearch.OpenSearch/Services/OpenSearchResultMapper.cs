using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using OpenSearch.Client;

namespace Bielu.BetterSearch.OpenSearch.Services;

/// <summary>
/// Maps hits to a <see cref="SearchHit"/> with their score, holding a <see cref="SearchDocument"/>: the document id
/// unchanged, its type, the query's index and its fields, a field stored as <c>null</c> becoming an empty list and
/// <c>null</c> values left out, and its highlights keyed by the query model's field names; and the query's page.
/// </summary>
internal sealed class OpenSearchResultMapper : IResultMapper<ISearchResponse<SearchDocument>>
{
    public ISearchResult MapResult(ISearchResponse<SearchDocument> searchEngineResult, ISearchQuery query)
    {
        var pageSize = OpenSearchSearchProviderAsync.PageSizeOf(query);
        return new BaseSearchResults
        {
            Items = searchEngineResult.Hits.Select(hit => MapHit(hit, query.Index)).ToList(),
            Pagination = new Pagination
            {
                TotalCount = searchEngineResult.Total,
                Page = OpenSearchSearchProviderAsync.PageOf(query),
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(searchEngineResult.Total / (double)pageSize)
            }
        };
    }

    private static ISearchHit MapHit(IHit<SearchDocument> hit, string index) => new SearchHit(
        StoredFields.ToDocument(hit.Id ?? hit.Source?.Id ?? string.Empty, hit.Source?.Type, index, hit.Source?.Fields),
        hit.Score, Highlights.ToHit(hit.Highlight, OpenSearchFieldNames.ToModel));
}
