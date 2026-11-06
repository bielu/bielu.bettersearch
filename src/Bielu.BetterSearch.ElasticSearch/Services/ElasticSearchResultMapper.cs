using System.Text.Json;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;

namespace Bielu.BetterSearch.ElasticSearch.Services;

/// <summary>
/// Maps hits to <see cref="BaseSearchModel"/>: the document id (unchanged in <c>Key</c>, as
/// <c>Id</c> lower-cases it), its score, its type as
/// <c>ContentType</c> and its fields as <c>CustomProperties</c>.
/// </summary>
public class ElasticSearchResultMapper : IResultMapper<SearchResponse<SearchDocument>>
{
    public ISearchResult<ISearchModel> MapResult(SearchResponse<SearchDocument> searchEngineResult) => new BaseSearchResults
    {
        Items = searchEngineResult.Hits.Select(MapHit).ToList(),
        Pagination = new Pagination { TotalCount = searchEngineResult.Total }
    };

    private static ISearchModel MapHit(Hit<SearchDocument> hit) => new BaseSearchModel
    {
        Id = hit.Id ?? hit.Source?.Id ?? string.Empty,
        Key = hit.Id ?? hit.Source?.Id ?? string.Empty,
        Score = hit.Score,
        ContentType = hit.Source?.Type ?? string.Empty,
        CustomProperties = hit.Source?.Fields.ToDictionary(
            field => field.Key,
            field => field.Value.Select(ToClrValue).ToList()) ?? []
    };

    /// <summary>Source values are deserialized as <see cref="JsonElement"/>; callers get plain values.</summary>
    private static object ToClrValue(object? value) => value switch
    {
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString()!,
        JsonElement { ValueKind: JsonValueKind.Number } element => element.TryGetInt64(out var number) ? number : element.GetDouble(),
        JsonElement { ValueKind: JsonValueKind.True } => true,
        JsonElement { ValueKind: JsonValueKind.False } => false,
        JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null!,
        JsonElement element => element.Clone(),
        _ => value!
    };
}
