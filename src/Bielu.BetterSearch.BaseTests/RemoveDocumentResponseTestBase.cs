using Bielu.BetterSearch.Abstractions.Services;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

/// <summary>
/// <c>RemoveDocumentAsync</c> against the delete responses Elasticsearch and OpenSearch share, served without a
/// cluster. Inherited by both providers' response tests, so the payloads and the expected outcome stay the same.
/// </summary>
public abstract class RemoveDocumentResponseTestBase
{
    /// <summary>A delete of a document the index does not hold: HTTP 404 with a <c>not_found</c> result.</summary>
    protected const string DocumentNotFoundResponse =
        """{"_index":"people","_id":"1","_version":1,"result":"not_found","_shards":{"total":2,"successful":1,"failed":0},"_seq_no":0,"_primary_term":1}""";

    /// <summary>A delete in an index that does not exist: HTTP 404 with an <c>index_not_found_exception</c> error.</summary>
    protected const string IndexNotFoundResponse =
        """{"error":{"root_cause":[{"type":"index_not_found_exception","reason":"no such index [people]"}],"type":"index_not_found_exception","reason":"no such index [people]"},"status":404}""";

    /// <summary>An indexing provider whose client answers every request with <paramref name="json"/> and <paramref name="statusCode"/>.</summary>
    protected abstract IIndexingProviderAsync ProviderAnswering(string json, int statusCode);

    [Fact]
    public async Task RemoveDocumentAsync_DocumentNotFound_ReturnsSuccess()
    {
        var provider = ProviderAnswering(DocumentNotFoundResponse, 404);

        var result = await provider.RemoveDocumentAsync("1", "people", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveDocumentAsync_IndexNotFound_ReturnsFailure()
    {
        var provider = ProviderAnswering(IndexNotFoundResponse, 404);

        var result = await provider.RemoveDocumentAsync("1", "people", TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        // The part of the client's debug information both clients write, whether or not they keep the response body.
        result.Errors.Single().Message.ShouldContain("(404) low level call on DELETE: /people/_doc/1");
    }
}
