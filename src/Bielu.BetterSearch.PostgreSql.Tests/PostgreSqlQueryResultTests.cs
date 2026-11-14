using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.PostgreSql.Tests;

/// <summary>Each write commits before it returns, so documents are searchable once indexed.</summary>
public sealed class PostgreSqlQueryResultTests(
    ISearchServiceAsync serviceAsync,
    IIndexingServiceAsync indexingServiceAsync,
    IIndexingProviderAsync indexingProviderAsync)
    : QueryResultBaseTests(serviceAsync, indexingServiceAsync, indexingProviderAsync);
