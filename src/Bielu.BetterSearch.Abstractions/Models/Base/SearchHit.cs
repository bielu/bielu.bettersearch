namespace Bielu.BetterSearch.Abstractions.Models;

public sealed class SearchHit(ISearchModel document, double? score) : ISearchHit
{
    public ISearchModel Document { get; } = document;
    public double? Score { get; } = score;
}
