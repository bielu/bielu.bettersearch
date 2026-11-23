namespace Bielu.BetterSearch.Abstractions.Query.Aggregations;

/// <summary>One field of a <see cref="MultiTermAggregation"/>.</summary>
public class SimpleTerm
{
    /// <summary>The name of the field whose values are counted.</summary>
    public required string TermFieldName { get; set; }
}
