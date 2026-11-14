namespace Bielu.BetterSearch.Abstractions.Models;

/// <summary>
/// A rebuild that <c>StartRebuildAsync</c> began: a new index that is filled while searches and writes keep using
/// the current one, until <c>CompleteRebuildAsync</c> points the alias at it.
/// </summary>
/// <param name="Index">The index name searches and writes use, without the provider's index prefix.</param>
/// <param name="TargetIndex">The new index the rebuild fills, as the engine names it, prefix included.</param>
public sealed record IndexRebuild(string Index, string TargetIndex);
