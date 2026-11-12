using System.Collections.ObjectModel;

namespace Bielu.BetterSearch.Abstractions.Models;

public sealed class SearchHit(
    ISearchModel document,
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

    public ISearchModel Document { get; } = document;
    public double? Score { get; } = score;
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Highlights { get; } = highlights ?? NoHighlights;
}
