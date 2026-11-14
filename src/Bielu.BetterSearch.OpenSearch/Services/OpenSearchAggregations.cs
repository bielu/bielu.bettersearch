using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.Services;
using FluentResults;
using OpenSearch.Client;
using IAggregation = Bielu.BetterSearch.Abstractions.Models.IAggregation;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services;

/// <summary>
/// Translates <c>FacetQueries</c> into OpenSearch aggregations and reads their results back. A term facet is a
/// <c>terms</c> aggregation, a multi-term facet a <c>multi_terms</c> aggregation and a filter facet a <c>filter</c>
/// aggregation, each with its nested facets as sub-aggregations. A field mapped as text is counted on its keyword
/// sub-field, as sorting is.
/// </summary>
internal static class OpenSearchAggregations
{
    /// <summary>The start of the message OpenSearch gives for an aggregation on a text field.</summary>
    private const string TextFieldError = "Text fields are not optimised";

    public static async Task<Result<AggregationDictionary?>> TranslateAsync(ISearchQuery query,
        IReadOnlyList<OpenSearchFieldMapping> mappings, IQueryTranslateServiceAsync<QueryContainer> queryTranslateService)
    {
        if (query.FacetQueries is not { Count: > 0 })
        {
            return Result.Ok<AggregationDictionary?>(null);
        }

        var validated = Facets.Validate(query);
        if (validated.IsFailed)
        {
            return validated.ToResult<AggregationDictionary?>();
        }

        var translated = await TranslateAsync(query.Index, query.FacetQueries, mappings, queryTranslateService).ConfigureAwait(false);
        return translated.IsSuccess
            ? Result.Ok<AggregationDictionary?>(translated.Value)
            : translated.ToResult<AggregationDictionary?>();
    }

    private static async Task<Result<AggregationDictionary>> TranslateAsync(string index, IEnumerable<IAggregationQuery> facets,
        IReadOnlyList<OpenSearchFieldMapping> mappings, IQueryTranslateServiceAsync<QueryContainer> queryTranslateService)
    {
        var aggregations = new AggregationDictionary();
        foreach (var facet in facets)
        {
            BucketAggregationBase aggregation;
            switch (facet)
            {
                case TermAggregation term:
                    aggregation = new TermsAggregation(facet.AggregationName)
                    {
                        Field = Field(term.TermFieldName, mappings),
                        Size = Facets.SizeOf(term.Size)
                    };
                    break;
                case MultiTermAggreation multiTerm:
                    aggregation = new MultiTermsAggregation(facet.AggregationName)
                    {
                        Terms = multiTerm.Terms.Select(term => new Term { Field = Field(term.TermFieldName, mappings) }).ToList(),
                        Size = Facets.SizeOf(multiTerm.Size)
                    };
                    break;
                case FilterAggregationQuery filter:
                    var filterQuery = new SearchQuery { Index = index };
                    foreach (var (occurance, subQuery) in filter.Queries ?? [])
                    {
                        filterQuery.Add(occurance, subQuery);
                    }

                    var translated = await queryTranslateService.TranslateMainQueryAsync(filterQuery).ConfigureAwait(false);
                    if (translated.IsFailed)
                    {
                        return translated.ToResult<AggregationDictionary>();
                    }

                    aggregation = new FilterAggregation(facet.AggregationName) { Filter = translated.Value };
                    break;
                default:
                    return Result.Fail($"Facet {facet.AggregationName} is a {facet.GetType().Name}, which the OpenSearch provider does not support.");
            }

            if (facet.NestedAggregations is { Count: > 0 })
            {
                var nested = await TranslateAsync(index, facet.NestedAggregations, mappings, queryTranslateService).ConfigureAwait(false);
                if (nested.IsFailed)
                {
                    return nested;
                }

                aggregation.Aggregations = nested.Value;
            }

            aggregations.Add(facet.AggregationName, aggregation);
        }

        return Result.Ok<AggregationDictionary>(aggregations);
    }

    // Facets count the keyword sub-field of a field mapped as text, the same field sorting uses.
    private static string Field(string field, IReadOnlyList<OpenSearchFieldMapping> mappings) =>
        FieldMappings.SortField(OpenSearchFieldNames.Resolve(field), mappings);

    /// <summary>
    /// The message for a search that failed because a facet counts a text field, or <c>null</c> when it failed for
    /// another reason.
    /// </summary>
    public static string? TextFieldFailure(string failure) =>
        failure.Contains(TextFieldError, StringComparison.Ordinal)
            ? "A facet counts a field OpenSearch stores as text only, which it can't count. Map the field as Text in Mappings, " +
              "so the facet counts its keyword sub-field, or as Keyword; or facet on <field>.keyword when dynamic mapping " +
              $"created the field. {failure}"
            : null;

    /// <summary>The results of <paramref name="facets"/>, in their order, read from <paramref name="aggregates"/>.</summary>
    public static Result<IReadOnlyList<IAggregation>> Map(IEnumerable<IAggregationQuery> facets, AggregateDictionary? aggregates)
    {
        var results = new List<IAggregation>();
        foreach (var facet in facets)
        {
            var nestedFacets = facet.NestedAggregations ?? [];
            var mapped = facet switch
            {
                TermAggregation => aggregates?.Terms(facet.AggregationName) is { } terms
                    ? Buckets(facet, terms.Buckets.Select(bucket => (bucket.KeyAsString ?? bucket.Key, bucket.DocCount, (AggregateDictionary)bucket)), nestedFacets)
                    : Missing(facet),
                MultiTermAggreation => aggregates?.MultiTerms(facet.AggregationName) is { } multiTerms
                    ? MultiTermBuckets(facet, multiTerms, nestedFacets)
                    : Missing(facet),
                FilterAggregationQuery => aggregates?.Filter(facet.AggregationName) is { } filter
                    ? Map(nestedFacets, filter).Map(nested => (IAggregation)Facets.Filter(facet.AggregationName, filter.DocCount, nested))
                    : Missing(facet),
                _ => Result.Fail<IAggregation>($"Facet {facet.AggregationName} is a {facet.GetType().Name}, which the OpenSearch provider does not support.")
            };
            if (mapped.IsFailed)
            {
                return mapped.ToResult<IReadOnlyList<IAggregation>>();
            }

            results.Add(mapped.Value);
        }

        return Result.Ok<IReadOnlyList<IAggregation>>(results);
    }

    private static Result<IAggregation> Missing(IAggregationQuery facet) =>
        Result.Fail<IAggregation>($"OpenSearch returned no result for facet {facet.AggregationName}.");

    private static Result<IAggregation> Buckets(IAggregationQuery facet, IEnumerable<(string Key, long? DocCount, AggregateDictionary Aggregations)> buckets,
        IReadOnlyCollection<IAggregationQuery> nestedFacets)
    {
        var aggregation = new BucketAggregation { AggregationName = facet.AggregationName };
        foreach (var (key, docCount, subAggregations) in buckets)
        {
            var nested = Map(nestedFacets, subAggregations);
            if (nested.IsFailed)
            {
                return nested.ToResult<IAggregation>();
            }

            aggregation.Buckets.Add(Facets.TermBucket(key, docCount, nested.Value));
        }

        return Result.Ok<IAggregation>(aggregation);
    }

    private static Result<IAggregation> MultiTermBuckets(IAggregationQuery facet, MultiTermsAggregate<string> multiTerms, IReadOnlyCollection<IAggregationQuery> nestedFacets)
    {
        var aggregation = new BucketAggregation { AggregationName = facet.AggregationName };
        foreach (var bucket in multiTerms.Buckets)
        {
            var nested = Map(nestedFacets, bucket);
            if (nested.IsFailed)
            {
                return nested.ToResult<IAggregation>();
            }

            var keys = bucket.Key.ToList();
            aggregation.Buckets.Add(Facets.MultiTermBucket(bucket.KeyAsString ?? string.Join('|', keys), keys, bucket.DocCount, nested.Value));
        }

        return Result.Ok<IAggregation>(aggregation);
    }
}
