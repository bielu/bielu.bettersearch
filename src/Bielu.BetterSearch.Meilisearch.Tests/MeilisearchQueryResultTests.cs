using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Meilisearch.Tests;

/// <summary>Writes wait for their Meilisearch task, so documents are searchable once indexed.</summary>
public sealed class MeilisearchQueryResultTests(
    ISearchServiceAsync serviceAsync,
    IIndexingServiceAsync indexingServiceAsync,
    IIndexingProviderAsync indexingProviderAsync)
    : QueryResultBaseTests(serviceAsync, indexingServiceAsync, indexingProviderAsync);
