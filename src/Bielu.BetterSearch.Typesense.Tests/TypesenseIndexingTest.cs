using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Bielu.BetterSearch.Typesense.Services;
using Shouldly;

namespace Bielu.BetterSearch.Typesense.Tests;

public sealed class TypesenseIndexingTest(
    IIndexingServiceAsync serviceAsync,
    IIndexingProviderAsync indexingProviderAsync,
    IClientFactoryAsync<TypesenseHttpClientWrapper> clientFactory)
    : IndexingTestBase(serviceAsync, indexingProviderAsync)
{
    // The provider converts every value to the field type its schema declares for it, and Typesense accepted every
    // malformed document tried (an overflowing integer, an out-of-range point, a 1 MB id), so none is rejected.
    protected override SearchDocument? CreateDocumentRejectedByEngine(string index) => null;

    // Typesense applies each write before it answers, so the count already reflects it.
    protected override async Task<long> CountDocumentsAsync(string index)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index);
        var response = await client.SendAsync(HttpMethod.Get, $"collections/{index}", null, TestContext.Current.CancellationToken);
        response.IsSuccess.ShouldBeTrue(response.Describe());
        using var json = System.Text.Json.JsonDocument.Parse(response.Body);
        return json.RootElement.GetProperty("num_documents").GetInt64();
    }
}
