using System.Globalization;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using FluentResults;
using EsQuery = Elastic.Clients.Elasticsearch.QueryDsl.Query;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.ElasticSearch.Services;

/// <summary>
/// Translates <c>FacetQueries</c> into Elasticsearch aggregations and reads their results back. A term facet is a
/// <c>terms</c> aggregation, a multi-term facet a <c>multi_terms</c> aggregation and a filter facet a <c>filter</c>
/// aggregation, each with its nested facets as sub-aggregations. A field mapped as text is counted on its keyword
/// sub-field, as sorting is.
/// </summary>
internal static class ElasticSearchAggregations
{
    /// <summary>The start of the message Elasticsearch gives for an aggregation on a text field.</summary>
    private const string TextFieldError = "Text fields are not optimised";

    public static async Task<Result<IDictionary<string, Aggregation>?>> TranslateAsync(ISearchQuery query,
        IReadOnlyList<ElasticSearchFieldMapping> mappings, IQueryTranslateServiceAsync<EsQuery> queryTranslateService)
    {
        if (query.FacetQueries is not { Count: > 0 })
        {
            return Result.Ok<IDictionary<string, Aggregation>?>(null);
        }

        var validated = Facets.Validate(query);
        if (validated.IsFailed)
        {
            return validated.ToResult<IDictionary<string, Aggregation>?>();
        }

        var translated = await TranslateAsync(query.Index, query.FacetQueries, mappings, queryTranslateService).ConfigureAwait(false);
        return translated.IsSuccess
            ? Result.Ok<IDictionary<string, Aggregation>?>(translated.Value)
            : translated.ToResult<IDictionary<string, Aggregation>?>();
    }

    private static async Task<Result<IDictionary<string, Aggregation>>> TranslateAsync(string index, IEnumerable<IAggregationQuery> facets,
        IReadOnlyList<ElasticSearchFieldMapping> mappings, IQueryTranslateServiceAsync<EsQuery> queryTranslateService)
    {
        var aggregations = new Dictionary<string, Aggregation>(StringComparer.Ordinal);
        foreach (var facet in facets)
        {
            var aggregation = new Aggregation();
            switch (facet)
            {
                case TermAggregation term:
                    aggregation.Terms = new TermsAggregation
                    {
                        Field = Field(term.TermFieldName, mappings),
                        Size = Facets.SizeOf(term.Size)
                    };
                    break;
                case MultiTermAggreation multiTerm:
                    aggregation.MultiTerms = new MultiTermsAggregation
                    {
                        Terms = multiTerm.Terms.Select(term => new MultiTermLookup { Field = Field(term.TermFieldName, mappings) }).ToList(),
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
                        return translated.ToResult<IDictionary<string, Aggregation>>();
                    }

                    aggregation.Filter = translated.Value;
                    break;
                default:
                    return Result.Fail($"Facet {facet.AggregationName} is a {facet.GetType().Name}, which the Elasticsearch provider does not support.");
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

            aggregations[facet.AggregationName] = aggregation;
        }

        return Result.Ok<IDictionary<string, Aggregation>>(aggregations);
    }

    // Facets count the keyword sub-field of a field mapped as text, the same field sorting uses.
    private static string Field(string field, IReadOnlyList<ElasticSearchFieldMapping> mappings) =>
        FieldMappings.SortField(ElasticSearchFieldNames.Resolve(field), mappings);

    /// <summary>
    /// The message for a search that failed because a facet counts a text field, or <c>null</c> when it failed for
    /// another reason.
    /// </summary>
    public static string? TextFieldFailure(string failure) =>
        failure.Contains(TextFieldError, StringComparison.Ordinal)
            ? "A facet counts a field Elasticsearch stores as text only, which it can't count. Map the field as Text in Mappings, " +
              "so the facet counts its keyword sub-field, or as Keyword; or facet on <field>.keyword when dynamic mapping " +
              $"created the field. {failure}"
            : null;

    /// <summary>The results of <paramref name="facets"/>, in their order, read from <paramref name="aggregates"/>.</summary>
    public static Result<IReadOnlyList<IAggregation>> Map(IEnumerable<IAggregationQuery> facets, AggregateDictionary? aggregates)
    {
        var results = new List<IAggregation>();
        foreach (var facet in facets)
        {
            IAggregate? aggregate = null;
            if (aggregates?.TryGetAggregate(facet.AggregationName, out aggregate) != true || aggregate == null)
            {
                return Result.Fail($"Elasticsearch returned no result for facet {facet.AggregationName}.");
            }

            var nestedFacets = facet.NestedAggregations ?? [];
            var mapped = (facet, aggregate) switch
            {
                (TermAggregation, StringTermsAggregate terms) => Buckets(facet, terms.Buckets.Select(bucket => (Key(bucket.Key), bucket.DocCount, bucket.Aggregations)), nestedFacets),
                (TermAggregation, LongTermsAggregate terms) => Buckets(facet, terms.Buckets.Select(bucket => (bucket.KeyAsString ?? Facets.Key(bucket.Key), bucket.DocCount, bucket.Aggregations)), nestedFacets),
                (TermAggregation, DoubleTermsAggregate terms) => Buckets(facet, terms.Buckets.Select(bucket => (bucket.KeyAsString ?? Facets.Key(bucket.Key), bucket.DocCount, bucket.Aggregations)), nestedFacets),
                // A field no document has yet.
                (TermAggregation or MultiTermAggreation, UnmappedTermsAggregate) => Result.Ok<IAggregation>(new BucketAggregation { AggregationName = facet.AggregationName }),
                (MultiTermAggreation, MultiTermsAggregate multiTerms) => MultiTermBuckets(facet, multiTerms, nestedFacets),
                (FilterAggregationQuery, FilterAggregate filter) => Map(nestedFacets, filter.Aggregations)
                    .Map(nested => (IAggregation)Facets.Filter(facet.AggregationName, filter.DocCount, nested)),
                _ => Result.Fail<IAggregation>($"Elasticsearch returned a {aggregate.GetType().Name} for facet {facet.AggregationName}, which is a {facet.GetType().Name}.")
            };
            if (mapped.IsFailed)
            {
                return mapped.ToResult<IReadOnlyList<IAggregation>>();
            }

            results.Add(mapped.Value);
        }

        return Result.Ok<IReadOnlyList<IAggregation>>(results);
    }

    private static Result<IAggregation> Buckets(IAggregationQuery facet, IEnumerable<(string Key, long DocCount, AggregateDictionary? Aggregations)> buckets,
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

    private static Result<IAggregation> MultiTermBuckets(IAggregationQuery facet, MultiTermsAggregate multiTerms, IReadOnlyCollection<IAggregationQuery> nestedFacets)
    {
        var aggregation = new BucketAggregation { AggregationName = facet.AggregationName };
        foreach (var bucket in multiTerms.Buckets)
        {
            var nested = Map(nestedFacets, bucket.Aggregations);
            if (nested.IsFailed)
            {
                return nested.ToResult<IAggregation>();
            }

            var keys = bucket.Key.Select(Key).ToList();
            aggregation.Buckets.Add(Facets.MultiTermBucket(bucket.KeyAsString ?? string.Join('|', keys), keys, bucket.DocCount, nested.Value));
        }

        return Result.Ok<IAggregation>(aggregation);
    }

    private static string Key(FieldValue key) => key.Value switch
    {
        string text => text,
        long number => Facets.Key(number),
        double number => Facets.Key(number),
        bool flag => flag ? "true" : "false",
        null => string.Empty,
        var other => Convert.ToString(other, CultureInfo.InvariantCulture) ?? string.Empty
    };
}
