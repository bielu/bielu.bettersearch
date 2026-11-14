using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Services;
using FluentResults;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.Meilisearch.Services;

/// <summary>
/// Term facets through Meilisearch's <c>facets</c>, on the <c>fields</c> attribute that is already filterable.
/// Meilisearch counts values, not combinations of them, and has no sub-aggregations, so multi-term, filter and nested
/// facets fail.
/// </summary>
internal static class MeilisearchFacets
{
    public const string UnsupportedComplexFacets = "The Meilisearch provider counts only term facets without NestedAggregations";

    /// <summary>
    /// Meilisearch's default <c>maxValuesPerFacet</c>: it returns at most this many values of a facet, in alphabetical
    /// order, so when it returns this many it may have left out values with more documents.
    /// </summary>
    public const int MaxValuesPerFacet = 100;

    /// <summary>The term facets of the query and the attribute each one counts; empty when it has none.</summary>
    public static Result<IReadOnlyList<(TermAggregation Facet, string Attribute)>> Requested(ISearchQuery query)
    {
        if (query.FacetQueries is not { Count: > 0 })
        {
            return Result.Ok<IReadOnlyList<(TermAggregation, string)>>([]);
        }

        var validated = Facets.Validate(query);
        if (validated.IsFailed)
        {
            return validated.ToResult<IReadOnlyList<(TermAggregation, string)>>();
        }

        var requested = new List<(TermAggregation, string)>();
        foreach (var facet in query.FacetQueries)
        {
            if (facet is not TermAggregation term)
            {
                return Result.Fail($"{UnsupportedComplexFacets}: facet {facet.AggregationName} is a {facet.GetType().Name}.");
            }

            if (term.NestedAggregations is { Count: > 0 })
            {
                return Result.Fail($"{UnsupportedComplexFacets}: facet {facet.AggregationName} has NestedAggregations.");
            }

            var field = MeilisearchFieldNames.Require(term.TermFieldName, "TermAggregation");
            if (field.IsFailed)
            {
                return field.ToResult<IReadOnlyList<(TermAggregation, string)>>();
            }

            requested.Add((term, MeilisearchFieldNames.In(MeilisearchFieldNames.Fields, field.Value)));
        }

        return Result.Ok<IReadOnlyList<(TermAggregation, string)>>(requested);
    }

    /// <summary>
    /// The facet results, in the query's order, from Meilisearch's <c>facetDistribution</c>: the values with the most
    /// documents first, ties in ordinal order of the value, cut to the facet's <c>Size</c>.
    /// </summary>
    public static Result<IReadOnlyList<IAggregation>> Map(IReadOnlyList<(TermAggregation Facet, string Attribute)> requested,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>? distribution)
    {
        var results = new List<IAggregation>();
        foreach (var (facet, attribute) in requested)
        {
            IReadOnlyDictionary<string, int>? counts = null;
            distribution?.TryGetValue(attribute, out counts);
            counts ??= new Dictionary<string, int>();
            if (counts.Count >= MaxValuesPerFacet)
            {
                return Result.Fail<IReadOnlyList<IAggregation>>(
                    $"Facet {facet.AggregationName} has {MaxValuesPerFacet} or more values, Meilisearch's maxValuesPerFacet, which returns them in alphabetical order, so the values with the most documents may be missing. Narrow the query or facet a field with fewer values.");
            }

            var aggregation = new BucketAggregation { AggregationName = facet.AggregationName };
            aggregation.Buckets.AddRange(counts
                .OrderByDescending(count => count.Value)
                .ThenBy(count => count.Key, StringComparer.Ordinal)
                .Take(Facets.SizeOf(facet.Size))
                .Select(count => Facets.TermBucket(count.Key, count.Value, [])));
            results.Add(aggregation);
        }

        return Result.Ok<IReadOnlyList<IAggregation>>(results);
    }
}
