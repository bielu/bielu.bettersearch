using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Lifti.Services;
using FluentResults;
using Lifti;
using Shouldly;

namespace Bielu.BetterSearch.Lifti.Tests;

/// <summary>Exceptions thrown by a custom index manager or client factory, which the default ones never throw.</summary>
public class IndexingExceptionUnitTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string> IndexingMethods =>
    [
        nameof(IIndexingProviderAsync.IndexDocumentAsync),
        nameof(IIndexingProviderAsync.IndexMultipleDocumentsAsync),
        nameof(IIndexingProviderAsync.RemoveDocumentAsync),
        nameof(IIndexingProviderAsync.RemoveDocumentsAsync),
        nameof(IIndexingProviderAsync.RemoveAllDocumentsAsync),
        nameof(IIndexingProviderAsync.EnsureIndexExistsAsync),
        nameof(IIndexingProviderAsync.DeleteIndexAsync),
        nameof(IIndexingProviderAsync.IndexExistsAsync),
        nameof(IIndexingProviderAsync.CreateIndexAsync),
    ];

    private static async Task<IResultBase> InvokeAsync(LiftiIndexingProviderAsync provider, string method, CancellationToken cancellationToken)
    {
        var document = new SearchDocument { Id = "1", Index = "test-index" };
        return method switch
        {
            nameof(IIndexingProviderAsync.IndexDocumentAsync) => await provider.IndexDocumentAsync(document, cancellationToken),
            nameof(IIndexingProviderAsync.IndexMultipleDocumentsAsync) => await provider.IndexMultipleDocumentsAsync([document], cancellationToken),
            nameof(IIndexingProviderAsync.RemoveDocumentAsync) => await provider.RemoveDocumentAsync("1", "test-index", cancellationToken),
            nameof(IIndexingProviderAsync.RemoveDocumentsAsync) => await provider.RemoveDocumentsAsync("test-index", ["1", "2"], cancellationToken),
            nameof(IIndexingProviderAsync.RemoveAllDocumentsAsync) => await provider.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = "test-index" }, cancellationToken),
            nameof(IIndexingProviderAsync.EnsureIndexExistsAsync) => await provider.EnsureIndexExistsAsync("test-index", cancellationToken),
            nameof(IIndexingProviderAsync.DeleteIndexAsync) => await provider.DeleteIndexAsync("test-index", cancellationToken),
            nameof(IIndexingProviderAsync.IndexExistsAsync) => await provider.IndexExistsAsync("test-index", cancellationToken),
            nameof(IIndexingProviderAsync.CreateIndexAsync) => await provider.CreateIndexAsync("test-index", cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null),
        };
    }

    [Theory]
    [MemberData(nameof(IndexingMethods))]
    public async Task IndexingMethod_ManagerAndFactoryThrow_ReturnsFailureCausedByTheException(string method)
    {
        var exception = new InvalidOperationException("index store unavailable");
        var provider = new LiftiIndexingProviderAsync(new ThrowingClientFactory(exception), new ThrowingLiftiIndexManager(exception));

        var result = await InvokeAsync(provider, method, Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Select(e => e.Message).ShouldBe(["index store unavailable"]);
        result.Errors[0].Reasons.OfType<ExceptionalError>().Single().Exception.ShouldBeSameAs(exception);
    }

    [Theory]
    [MemberData(nameof(IndexingMethods))]
    public async Task IndexingMethod_ManagerAndFactoryCancelled_ThrowsOperationCanceledException(string method)
    {
        var exception = new OperationCanceledException();
        var provider = new LiftiIndexingProviderAsync(new ThrowingClientFactory(exception), new ThrowingLiftiIndexManager(exception));

        await Should.ThrowAsync<OperationCanceledException>(() => InvokeAsync(provider, method, Token));
    }

    [Fact]
    public async Task CreateIndexAsync_ManagerFailsWithManyErrors_ReturnsEveryError()
    {
        var errors = new[] { new Error("first").WithMetadata("code", 1), new Error("second").WithMetadata("code", 2) };
        var provider = new LiftiIndexingProviderAsync(new ThrowingClientFactory(new InvalidOperationException("unused")), new FailingCreateLiftiIndexManager(errors));

        var result = await provider.CreateIndexAsync("test-index", Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldBe(errors);
        result.Errors[1].Metadata["code"].ShouldBe(2);
    }

    private sealed class ThrowingClientFactory(Exception exception) : IClientFactoryAsync<IFullTextIndex<string>>
    {
        public Task<IFullTextIndex<string>> GetOrCreateClientAsync(string indexName) => throw exception;
    }

    private sealed class ThrowingLiftiIndexManager(Exception exception) : ILiftiIndexManager
    {
        public Task<Result<IFullTextIndex<string>>> GetOrCreateIndexAsync(string key) => throw exception;
        public Task<Result<IFullTextIndex<string>?>> CreateIndexAsync(string key) => throw exception;
        public Task<Result<IFullTextIndex<string>?>> GetIndexAsync(string key) => throw exception;
        public Task<Result<bool>> ExistsAsync(string name) => throw exception;
        public Task<Result<bool>> DeleteIndexAsync(string name) => throw exception;
    }

    private sealed class FailingCreateLiftiIndexManager(IEnumerable<IError> errors) : ILiftiIndexManager
    {
        public Task<Result<IFullTextIndex<string>>> GetOrCreateIndexAsync(string key) => throw new NotSupportedException();
        public Task<Result<IFullTextIndex<string>?>> CreateIndexAsync(string key) => Task.FromResult(Result.Fail<IFullTextIndex<string>?>(errors));
        public Task<Result<IFullTextIndex<string>?>> GetIndexAsync(string key) => throw new NotSupportedException();
        public Task<Result<bool>> ExistsAsync(string name) => throw new NotSupportedException();
        public Task<Result<bool>> DeleteIndexAsync(string name) => throw new NotSupportedException();
    }
}
