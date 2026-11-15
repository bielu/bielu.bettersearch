namespace Bielu.BetterSearch.Abstractions.Query;

public interface ISearchSubQuery
{
    /// <summary>
    /// How the clause combines with the others in its list: the only place a clause's occurrence is kept, at the
    /// top level of a query and inside a bool alike.
    /// </summary>
    Occurrence Occurrence { get; set; }

    /// <summary>The clause's relevance boost: 1 by default, and a fraction such as 0.5 or 1.5 is allowed. Providers that don't score ignore it.</summary>
    double BoostValue { get; set; }
}
