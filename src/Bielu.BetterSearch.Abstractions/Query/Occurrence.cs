namespace Bielu.BetterSearch.Abstractions.Query;

/// <summary>How a clause combines with the others in its list, as in a Lucene or Elasticsearch <c>bool</c> query.</summary>
public enum Occurrence
{
    /// <summary>A match must match the clause, and the clause adds to the score.</summary>
    Must = 1,
    /// <summary>A match should match the clause; with no <see cref="Must"/> or <see cref="Filter"/> clause beside it, at least one should clause must match.</summary>
    Should = 2,
    /// <summary>A match must not match the clause.</summary>
    MustNot = 3,
    /// <summary>A match must match the clause, which does not add to the score.</summary>
    Filter = 4
}
