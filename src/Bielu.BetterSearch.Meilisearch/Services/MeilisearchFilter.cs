namespace Bielu.BetterSearch.Meilisearch.Services;

/// <summary>A Meilisearch filter expression, such as <c>tokens.Name = "jane"</c>.</summary>
public sealed record MeilisearchFilter(string Expression);
