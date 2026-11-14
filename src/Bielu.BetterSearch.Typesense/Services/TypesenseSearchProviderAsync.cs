using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using Bielu.BetterSearch.Typesense.Configuration;
using FluentResults;
using Microsoft.Extensions.Options;
using N = Bielu.BetterSearch.Typesense.Services.TypesenseFieldNames;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.Typesense.Services;

/// <summary>
/// Runs the query as a Typesense filter with the search text <c>*</c>, so every hit matches it exactly and has the
/// same score, 1. Hits are ordered by <c>SortOrder</c> when given.
/// </summary>
public sealed class TypesenseSearchProviderAsync(
    IQueryTranslateServiceAsync<TypesenseSearchRequest> queryTranslateService,
    IClientFactoryAsync<TypesenseHttpClientWrapper> clientFactory,
    TypesenseSchema schema,
    IOptionsMonitor<TypesenseOptions> optionsMonitor) : ISearchProviderAsync
{
    /// <summary>Used when <c>PageSize</c> is not set, as for Elasticsearch.</summary>
    public const int DefaultPageSize = 10;

    /// <summary>The most hits Typesense returns in one page.</summary>
    public const int MaxPageSize = 250;

    /// <summary>The most fields Typesense sorts on.</summary>
    public const int MaxSortFields = 3;

    /// <summary>The score of every hit: each one matches the filter exactly, so none ranks above another.</summary>
    internal const double FilterScore = 1.0;

    internal const string UnsupportedFacets =
        "The Typesense provider does not support facets yet.";

    internal const string UnsupportedHighlighting =
        "The Typesense provider does not support highlighting yet: it runs every query as a filter with no search text, " +
        "and Typesense highlights only matches of the search text, so there is nothing for it to highlight.";

    public async Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(query.Index))
        {
            return Result.Fail<ISearchResult>("The query needs an Index.");
        }

        // Invalid options fail with the validator's messages, each naming its setting, before anything is sent.
        var options = optionsMonitor.TryCurrentValue();
        if (options.IsFailed)
        {
            return options.ToResult<ISearchResult>();
        }

        if (query.FacetQueries is { Count: > 0 })
        {
            return Result.Fail<ISearchResult>(UnsupportedFacets);
        }

        if (query.HighlightQueries is { Count: > 0 })
        {
            return Result.Fail<ISearchResult>(UnsupportedHighlighting);
        }

        var page = Math.Max(query.Page, 1);
        var pageSize = query.PageSize > 0 ? query.PageSize : DefaultPageSize;
        if (pageSize > MaxPageSize)
        {
            return Result.Fail<ISearchResult>($"PageSize {pageSize} is above {MaxPageSize}, the most hits Typesense returns in one page.");
        }

        var request = await queryTranslateService.TranslateMainQueryAsync(query).ConfigureAwait(false);
        if (request.IsFailed)
        {
            return request.ToResult<ISearchResult>();
        }

        var sortOrder = SortOrders.Ordered(query);
        if (sortOrder.IsFailed)
        {
            return sortOrder.ToResult<ISearchResult>();
        }

        var sortFields = new List<(string Name, bool Desc)>();
        foreach (var field in sortOrder.Value)
        {
            var name = N.Require(field.FieldName, "SortOrder");
            if (name.IsFailed)
            {
                return name.ToResult<ISearchResult>();
            }

            sortFields.Add((N.Name(name.Value), field.Desc));
        }

        if (sortFields.Count > MaxSortFields)
        {
            return Result.Fail<ISearchResult>($"SortOrder has {sortFields.Count} fields; Typesense sorts on at most {MaxSortFields}.");
        }

        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(query.Index).ConfigureAwait(false);
            var filterFields = request.Value.Filter.FieldNames().ToList();
            var refresh = false;
            for (var attempt = 0; ; attempt++)
            {
                var fields = await schema.FieldsAsync(client, query.Index, refresh, cancellationToken).ConfigureAwait(false);
                if (fields.IsFailed)
                {
                    return Result.Fail<ISearchResult>(new Error($"Typesense search failed: {fields.Errors[0].Message}"));
                }

                if (fields.Value is not { } known)
                {
                    return Result.Fail<ISearchResult>($"Typesense search failed: the index {query.Index} does not exist.");
                }

                // A field added since the fields were read would be missed, so a field they don't hold reads them again.
                if (!refresh && (filterFields.Any(field => !known.Contains(field))
                                 || sortFields.Any(sort => N.SortPrefixes.All(prefix => !known.Contains(prefix + sort.Name)))))
                {
                    refresh = true;
                    continue;
                }

                var rendered = request.Value.Filter.Render(known.Contains);
                if (rendered.IsFailed)
                {
                    return rendered.ToResult<ISearchResult>();
                }

                if (rendered.Value.MatchesNone)
                {
                    return Result.Ok(Results([], 0, page, pageSize)).WithSuccesses(request.Successes);
                }

                var search = new JsonObject
                {
                    ["collection"] = query.Index,
                    ["q"] = "*",
                    ["page"] = page,
                    ["per_page"] = pageSize,
                    ["include_fields"] = $"id,{N.Type},{N.Source}",
                };
                if (rendered.Value.Expression is { } filterBy)
                {
                    search["filter_by"] = filterBy;
                }

                var sortBy = SortBy(sortFields, known);
                if (sortBy.IsFailed)
                {
                    return sortBy.ToResult<ISearchResult>();
                }

                if (sortBy.Value is { } sortExpression)
                {
                    search["sort_by"] = sortExpression;
                }

                var body = new JsonObject { ["searches"] = new JsonArray(search) };
                var response = await client.SendAsync(HttpMethod.Post, "multi_search", () => JsonContent.Create(body), cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccess)
                {
                    return Result.Fail<ISearchResult>($"Typesense search failed: {response.Describe()}");
                }

                using var json = JsonDocument.Parse(response.Body);
                var result = json.RootElement.GetProperty("results")[0];
                if (result.TryGetProperty("error", out var error))
                {
                    var message = error.GetString() ?? "unknown error";
                    // The collection was recreated without a field the cached schema still held: read it again once.
                    if (attempt == 0 && message.StartsWith("Could not find a ", StringComparison.Ordinal))
                    {
                        schema.Forget(client, query.Index);
                        refresh = true;
                        continue;
                    }

                    var code = result.TryGetProperty("code", out var status) ? status.ToString() : "?";
                    return Result.Fail<ISearchResult>(new Error($"Typesense search failed ({code}): {message}").WithMetadata("code", code));
                }

                var found = result.GetProperty("found").GetInt64();
                var properties = query.Properties?.Where(property => !string.IsNullOrWhiteSpace(property)).Select(N.Name).ToHashSet(StringComparer.Ordinal);
                var hits = result.GetProperty("hits").EnumerateArray().Select(hit => MapHit(hit, properties)).ToList();
                return Result.Ok(Results(hits, found, page, pageSize)).WithSuccesses(request.Successes);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return Result.Fail<ISearchResult>(new Error($"Typesense search failed: {e.Message}").CausedBy(e));
        }
    }

    /// <summary>
    /// The <c>sort_by</c> for <paramref name="sortFields"/>, given the collection's <paramref name="known"/> fields, or
    /// <c>null</c> when no document has any of them. Integers and dates are kept apart from other numbers, so a field
    /// holding both can't be ordered as one and fails.
    /// </summary>
    internal static Result<string?> SortBy(IEnumerable<(string Name, bool Desc)> sortFields, IReadOnlySet<string> known)
    {
        var sortBy = new List<string>();
        foreach (var (name, desc) in sortFields)
        {
            if (known.Contains(N.SortInteger + name) && known.Contains(N.SortNumber + name))
            {
                return Result.Fail<string?>(
                    $"SortOrder field '{name}' holds an integer or date in some documents and a fractional number type (double, float or decimal) in others: " +
                    "Typesense stores them in separate fields, so it can't order them together. Index the field with one numeric type.");
            }

            if (N.SortPrefixes.FirstOrDefault(prefix => known.Contains(prefix + name)) is { } prefix)
            {
                sortBy.Add($"{prefix}{name}:{(desc ? "desc" : "asc")}");
            }
            else if (N.ValuePrefixes.Any(valuePrefix => known.Contains(valuePrefix + name)))
            {
                return Result.Fail<string?>(
                    $"SortOrder field '{name}' holds several values, or booleans, in every document: Typesense sorts only on a field holding one number, date or string.");
            }

            // No document has the field, so it orders nothing.
        }

        return Result.Ok<string?>(sortBy.Count > 0 ? string.Join(',', sortBy) : null);
    }

    private static ISearchResult Results(List<ISearchHit> hits, long found, int page, int pageSize) => new BaseSearchResults
    {
        Items = hits,
        Pagination = new Pagination
        {
            TotalCount = found,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(found / (double)pageSize)
        }
    };

    private static ISearchHit MapHit(JsonElement hit, HashSet<string>? properties)
    {
        var document = hit.GetProperty("document");
        var source = TypesenseDocument.ReadSource(document.TryGetProperty(N.Source, out var stored) ? stored.GetString() : null);
        if (source != null && properties is { Count: > 0 })
        {
            source = source.Where(field => properties.Contains(field.Key)).ToDictionary();
        }

        return new SearchHit(new BaseSearchModel
        {
            Id = document.GetProperty("id").GetString() ?? string.Empty,
            ContentType = document.TryGetProperty(N.Type, out var type) ? type.GetString() ?? string.Empty : string.Empty,
            CustomProperties = StoredFields.ToCustomProperties(source)
        }, FilterScore);
    }
}
