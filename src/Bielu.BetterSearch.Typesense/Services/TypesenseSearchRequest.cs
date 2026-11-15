namespace Bielu.BetterSearch.Typesense.Services;

/// <summary>A translated query, which the search provider sends to Typesense.</summary>
internal sealed record TypesenseSearchRequest
{
    internal TypesenseSearchRequest(TypesenseFilter filter) => Filter = filter;

    internal TypesenseFilter Filter { get; }
}
