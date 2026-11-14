using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Typesense.Tests;

/// <summary>Typesense applies each write before it answers, so documents are searchable once indexed.</summary>
public sealed class TypesenseQueryResultTests(
    ISearchServiceAsync serviceAsync,
    IIndexingServiceAsync indexingServiceAsync,
    IIndexingProviderAsync indexingProviderAsync)
    : QueryResultBaseTests(serviceAsync, indexingServiceAsync, indexingProviderAsync);
