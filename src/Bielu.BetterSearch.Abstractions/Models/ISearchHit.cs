namespace Bielu.BetterSearch.Abstractions.Models;

/// <summary>One document a search found, with what the engine says about the match.</summary>
public interface ISearchHit
{
    ISearchModel Document { get; }

    /// <summary>The relevance score the engine gave the hit, when it scores hits.</summary>
    double? Score { get; }
}
