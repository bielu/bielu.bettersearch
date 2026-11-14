using System.Globalization;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using FluentResults;

namespace Bielu.BetterSearch.Services;

/// <summary>Checks a query's <c>FacetQueries</c> and builds their results the same way for every provider.</summary>
internal static class Facets
{
    public const string NullEntry = "FacetQueries holds a null entry.";

    public const string MissingName = "A facet needs an AggregationName.";

    /// <summary>
    /// Fails on a <c>null</c> facet, a facet without a name, two facets of the same name side by side, a term facet
    /// without a field, a multi-term facet with fewer than two fields, a <c>Size</c> below 0 or above
    /// <see cref="TermAggregation.MaxSize"/>, and a facet type other than term, multi-term or filter, at every level
    /// of nesting.
    /// </summary>
    public static Result Validate(ISearchQuery query) => Validate(query.FacetQueries ?? [], "FacetQueries");

    private static Result Validate(IEnumerable<IAggregationQuery?> facets, string path)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var facet in facets)
        {
            if (facet == null)
            {
                return Result.Fail(path == "FacetQueries" ? NullEntry : $"{path} holds a null entry.");
            }

            if (string.IsNullOrWhiteSpace(facet.AggregationName))
            {
                return Result.Fail(MissingName);
            }

            var name = facet.AggregationName;
            if (!names.Add(name))
            {
                return Result.Fail($"Facet name {name} is used more than once at the same level.");
            }

            var own = facet switch
            {
                TermAggregation term => ValidateTerm(name, term),
                MultiTermAggreation multiTerm => ValidateMultiTerm(name, multiTerm),
                FilterAggregationQuery filter => ValidateFilter(name, filter),
                _ => Result.Fail($"Facet {name} is a {facet.GetType().Name}; only TermAggregation, MultiTermAggreation and FilterAggregationQuery are supported.")
            };
            if (own.IsFailed)
            {
                return own;
            }

            var nested = Validate(facet.NestedAggregations ?? [], $"The NestedAggregations of facet {name}");
            if (nested.IsFailed)
            {
                return nested;
            }
        }

        return Result.Ok();
    }

    private static Result ValidateTerm(string name, TermAggregation term)
    {
        if (string.IsNullOrWhiteSpace(term.TermFieldName))
        {
            return Result.Fail($"Facet {name} needs a TermFieldName.");
        }

        return ValidateSize(name, term.Size);
    }

    private static Result ValidateMultiTerm(string name, MultiTermAggreation multiTerm)
    {
        if (multiTerm.Terms is not { Count: >= 2 })
        {
            return Result.Fail($"Facet {name} needs at least two Terms; use a TermAggregation for one field.");
        }

        if (multiTerm.Terms.Any(term => string.IsNullOrWhiteSpace(term?.TermFieldName)))
        {
            return Result.Fail($"Every term of facet {name} needs a TermFieldName.");
        }

        return ValidateSize(name, multiTerm.Size);
    }

    private static Result ValidateFilter(string name, FilterAggregationQuery filter) =>
        filter.Queries?.Values.Any(subQuery => subQuery == null) == true
            ? Result.Fail($"The Queries of facet {name} hold a null query.")
            : Result.Ok();

    private static Result ValidateSize(string name, int size) => size switch
    {
        < 0 => Result.Fail($"Facet {name} has Size {size}; it must be 0, for the default of {TermAggregation.DefaultSize}, or more."),
        > TermAggregation.MaxSize => Result.Fail($"Facet {name} has Size {size}, past the limit of {TermAggregation.MaxSize} buckets (search.max_buckets)."),
        _ => Result.Ok()
    };

    /// <summary>The number of buckets to ask for: <paramref name="size"/>, or the default when it is 0.</summary>
    public static int SizeOf(int size) => size > 0 ? size : TermAggregation.DefaultSize;

    /// <summary>A bucket key the engine gave as a number, in the invariant culture, without losing digits.</summary>
    public static string Key(long key) => key.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc cref="Key(long)"/>
    public static string Key(double key) => key.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>The bucket of one value of a term facet.</summary>
    public static KeyBucket TermBucket(string key, long? count, IReadOnlyList<IAggregation> nested) => new()
    {
        Key = key,
        Keys = [key],
        TotalOfDocuments = count,
        Aggregations = nested,
        SubBuckets = SubBuckets(nested)
    };

    /// <summary>The bucket of one combination of a multi-term facet's values, keyed by the engine's joined key.</summary>
    public static KeyBucket MultiTermBucket(string key, IReadOnlyList<string> keys, long? count, IReadOnlyList<IAggregation> nested) => new()
    {
        Key = key,
        Keys = keys,
        TotalOfDocuments = count,
        IsComplex = true,
        Aggregations = nested,
        SubBuckets = SubBuckets(nested)
    };

    /// <summary>
    /// A filter facet's result: one bucket holding the number of matching documents, whose nested aggregations are
    /// both on the bucket and on the aggregation.
    /// </summary>
    public static SingleAggregation Filter(string name, long count, IReadOnlyList<IAggregation> nested) => new()
    {
        AggregationName = name,
        NestedAggregation = nested,
        Buckets =
        [
            new SingleBucket
            {
                Keys = [name],
                TotalOfDocuments = count,
                Aggregations = nested,
                SubBuckets = SubBuckets(nested)
            }
        ]
    };

    private static List<IBucket> SubBuckets(IReadOnlyList<IAggregation> nested) =>
        nested.SelectMany(aggregation => aggregation switch
        {
            BucketAggregation bucketAggregation => bucketAggregation.Buckets,
            SingleAggregation singleAggregation => singleAggregation.Buckets,
            _ => []
        }).ToList();
}
