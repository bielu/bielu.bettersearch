namespace Bielu.BetterSearch.Abstractions.Models;

/// <summary>Reads facet results by the <c>AggregationName</c> the facet query was given.</summary>
public static class AggregationExtensions
{
    /// <summary>The term or multi-term facet named <paramref name="name"/>, or <c>null</c> when there is none.</summary>
    public static BucketAggregation? RetrieveBucketAggregation(this ISearchResult result, string name) =>
        result.Aggregations.RetrieveBucketAggregation(name);

    /// <summary>The filter facet named <paramref name="name"/>, or <c>null</c> when there is none.</summary>
    public static SingleAggregation? RetrieveSingleAggregation(this ISearchResult result, string name) =>
        result.Aggregations.RetrieveSingleAggregation(name);

    /// <summary>
    /// The term or multi-term facet named <paramref name="name"/> among <paramref name="aggregations"/>, such as a
    /// bucket's nested aggregations, or <c>null</c> when there is none. Names are compared ordinally.
    /// </summary>
    public static BucketAggregation? RetrieveBucketAggregation(this IEnumerable<IAggregation> aggregations, string name) =>
        aggregations.OfType<BucketAggregation>().FirstOrDefault(aggregation => string.Equals(aggregation.AggregationName, name, StringComparison.Ordinal));

    /// <summary>The filter facet named <paramref name="name"/> among <paramref name="aggregations"/>, or <c>null</c> when there is none.</summary>
    public static SingleAggregation? RetrieveSingleAggregation(this IEnumerable<IAggregation> aggregations, string name) =>
        aggregations.OfType<SingleAggregation>().FirstOrDefault(aggregation => string.Equals(aggregation.AggregationName, name, StringComparison.Ordinal));
}
