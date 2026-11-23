using System.Collections.ObjectModel;

namespace Bielu.BetterSearch.Abstractions.Models;

/// <summary>One document a search found, with its score and highlights.</summary>
/// <param name="document">The document that matched.</param>
/// <param name="score">The relevance score the engine gave the hit, or <c>null</c> when it gave none.</param>
/// <param name="highlights">The highlighted fragments by field name, or <c>null</c> for none.</param>
public sealed class SearchHit(
    SearchDocument document,
    double? score,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? highlights = null) : ISearchHit
{
    /// <summary>The tag every provider puts before a highlighted term.</summary>
    public const string HighlightPreTag = "<em>";

    /// <summary>The tag every provider puts after a highlighted term.</summary>
    public const string HighlightPostTag = "</em>";

    // Read-only, because every hit without highlights shares it.
    internal static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> NoHighlights =
        ReadOnlyDictionary<string, IReadOnlyList<string>>.Empty;

    /// <inheritdoc />
    public SearchDocument Document { get; } = document;
    /// <inheritdoc />
    public double? Score { get; } = score;
    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Highlights { get; } = highlights ?? NoHighlights;
}
