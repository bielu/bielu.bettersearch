using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Abstractions.Services.NullImplementations;
using Bielu.BetterSearch.Services;
using FluentResults;
using Shouldly;

namespace Bielu.BetterSearch.Lifti.Tests;

/// <summary>
/// A provider written before <c>GetDocumentAsync</c>, <c>CountAsync</c> and <c>UpdateDocumentAsync</c> existed still
/// compiles, and the services return a failed <see cref="Result"/> naming it rather than throwing.
/// </summary>
public class DefaultInterfaceMembersUnitTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetDocumentAsync_ProviderWithoutIt_ReturnsFailedResult()
    {
        using var service = new SearchServiceAsync(new OldSearchProvider());

        var result = await service.GetDocumentAsync("index", "a", Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("OldSearchProvider does not support GetDocumentAsync.");
    }

    [Fact]
    public async Task CountAsync_ProviderWithoutIt_ReturnsFailedResult()
    {
        using var service = new SearchServiceAsync(new OldSearchProvider());

        var result = await service.CountAsync(new SearchQuery { Index = "index" }, Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("OldSearchProvider does not support CountAsync.");
    }

    [Fact]
    public async Task UpdateDocumentAsync_ProviderWithoutIt_ReturnsFailedResult()
    {
        using var service = new IndexingServiceAsync(new OldIndexingProvider(), new DummyDocumentValidatorAsync());

        var result = await service.UpdateDocumentAsync("index", "a",
            new Dictionary<string, IList<object>> { { "Title", ["new"] } }, Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("OldIndexingProvider does not support UpdateDocumentAsync.");
    }

    private sealed class OldSearchProvider : ISearchProviderAsync
    {
        public Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class OldIndexingProvider : IIndexingProviderAsync
    {
        public Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> documents, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest index, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<bool>> EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
