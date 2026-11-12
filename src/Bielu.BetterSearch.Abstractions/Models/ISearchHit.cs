namespace Bielu.BetterSearch.Abstractions.Models;

/// <summary>One document a search found, with what the engine says about the match.</summary>
public interface ISearchHit
{
    ISearchModel Document { get; }

    /// <summary>The relevance score the engine gave the hit, when it scores hits.</summary>
    double? Score { get; }

    /// <summary>
    /// The fragments of each field the query's <c>HighlightQueries</c> marked on this hit, keyed by field name, with
    /// matched terms between <see cref="SearchHit.HighlightPreTag"/> and <see cref="SearchHit.HighlightPostTag"/>.
    /// A field with no marked term is left out, so a hit with nothing highlighted has no entries.
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>> Highlights => SearchHit.NoHighlights;
}
