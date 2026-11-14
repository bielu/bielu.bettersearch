using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.PostgreSql.Tests;

public sealed class PostgreSqlDocumentOperationsTests(
    ISearchServiceAsync searchService,
    IIndexingServiceAsync indexingService,
    IIndexingProviderAsync indexingProvider)
    : DocumentOperationsTestBase(searchService, indexingService, indexingProvider)
{
    protected override bool StoresFields => true;
}
