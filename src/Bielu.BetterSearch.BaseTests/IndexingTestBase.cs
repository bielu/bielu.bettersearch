using Bielu.BetterSearch.Abstractions.Services;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

public abstract class IndexingTestBase(IIndexingServiceAsync serviceAsync, IIndexingProviderAsync indexingProviderAsync)
{
    [Fact]
    public async Task CanCreateIndexAsyncTest()
    {
        //Arrange
        var index = UniqueIndex();
        try
        {
            //Act
            var created = await indexingProviderAsync.CreateIndexAsync(index);

            //Assert
            ShouldSucceedWith(created, true);
            await IndexShouldExistAsync(index, true);
        }
        finally
        {
            await DeleteIndexQuietlyAsync(index);
        }
    }
    [Fact]
    public async Task CanDeleteIndexAsyncTest()
    {
        //Arrange
        var index = UniqueIndex();
        try
        {
            (await indexingProviderAsync.CreateIndexAsync(index)).IsSuccess.ShouldBeTrue();

            //Act
            var deleted = await indexingProviderAsync.DeleteIndexAsync(index);

            //Assert
            ShouldSucceedWith(deleted, true);
            await IndexShouldExistAsync(index, false);
        }
        finally
        {
            await DeleteIndexQuietlyAsync(index);
        }
    }
    [Fact]
    public async Task CanCheckIfIndexExistsAsyncTest()
    {
        //Arrange
        var index = UniqueIndex();
        try
        {
            (await indexingProviderAsync.CreateIndexAsync(index)).IsSuccess.ShouldBeTrue();

            //Act and Assert
            await IndexShouldExistAsync(index, true);
        }
        finally
        {
            await DeleteIndexQuietlyAsync(index);
        }
    }
    [Fact]
    public async Task IndexExistsAsync_MissingIndex_ReturnsFalse()
    {
        //Arrange
        var index = UniqueIndex();

        //Act and Assert
        await IndexShouldExistAsync(index, false);
    }
    [Fact]
    public async Task EnsureIndexWorksWhenIndexExistsAsyncTest()
    {
        //Arrange
        var index = UniqueIndex();
        try
        {
            (await indexingProviderAsync.CreateIndexAsync(index)).IsSuccess.ShouldBeTrue();

            //Act
            var ensured = await indexingProviderAsync.EnsureIndexExistsAsync(index);

            //Assert
            ShouldSucceedWith(ensured, true);
            await IndexShouldExistAsync(index, true);
        }
        finally
        {
            await DeleteIndexQuietlyAsync(index);
        }
    }
    [Fact]
    public async Task EnsureIndexWorksWhenIndexNotExistsAsyncTest()
    {
        //Arrange
        var index = UniqueIndex();
        try
        {
            //Act
            var ensured = await indexingProviderAsync.EnsureIndexExistsAsync(index);

            //Assert
            ShouldSucceedWith(ensured, true);
            await IndexShouldExistAsync(index, true);
        }
        finally
        {
            await DeleteIndexQuietlyAsync(index);
        }
    }
    [Fact]
    public async Task EnsureIndexExistsAsync_ConcurrentCallers_AllSucceedWithTrue()
    {
        //Arrange
        var index = UniqueIndex();
        try
        {
            //Act: callers that all see the index missing race to create it, and all but one find it already there.
            var results = await Task.WhenAll(Enumerable.Range(0, 8)
                .Select(_ => indexingProviderAsync.EnsureIndexExistsAsync(index, Token)));

            //Assert
            foreach (var result in results)
            {
                ShouldSucceedWith(result, true);
            }

            await IndexShouldExistAsync(index, true);
        }
        finally
        {
            await DeleteIndexQuietlyAsync(index);
        }
    }

    /// <summary>An index name no other test uses, so tests sharing an engine can't change what it holds.</summary>
    private static string UniqueIndex() => $"test-index-{Guid.NewGuid():N}";

    /// <summary>Checks <paramref name="result"/> succeeded, showing its errors when it did not, with <paramref name="expected"/> as its value.</summary>
    private static void ShouldSucceedWith(FluentResults.Result<bool> result, bool expected)
    {
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        result.Value.ShouldBe(expected);
    }

    /// <summary>Checks <c>IndexExistsAsync</c> succeeds, showing the provider's errors when it does not, and that it reads <paramref name="expected"/>.</summary>
    private async Task IndexShouldExistAsync(string index, bool expected)
    {
        var exists = await indexingProviderAsync.IndexExistsAsync(index);
        exists.IsSuccess.ShouldBeTrue(string.Join("; ", exists.Errors.Select(error => error.Message)));
        exists.Value.ShouldBe(expected);
    }

    /// <summary>Best-effort cleanup, so the shared engines don't keep the index; a failure here must not hide the test's own.</summary>
    private async Task DeleteIndexQuietlyAsync(string index)
    {
        try
        {
            await indexingProviderAsync.DeleteIndexAsync(index, Token);
        }
        catch (Exception)
        {
            // Ignored: the index may never have been created, or the engine may be unreachable.
        }
    }
    [Fact]
    public async Task IndexDocumentAsyncTest()
    {
        //Arrange
        var document = new SearchDocument { Id = "1", Type = "TestDocument", Index = "test-index-1", };

        //Act
        (await serviceAsync.IndexDocumentAsync(document)).IsSuccess.ShouldBeTrue();
    }
    [Fact]
    public async Task IndexDocumentAsync_NullField_Fails()
    {
        var document = new SearchDocument
        {
            Id = "1",
            Type = "TestDocument",
            Index = "test-index-1",
            Fields = new Dictionary<string, IList<object>> { { "Name", ["Jane"] }, { "Tags", null! } }
        };

        var result = await serviceAsync.IndexDocumentAsync(document);

        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("Tags");
    }
    [Fact]
    public async Task IndexDocumentAsync_NullFieldsDictionary_IndexesADocumentWithNoFields()
    {
        var document = new SearchDocument { Id = "1", Type = "TestDocument", Index = "test-index-1", Fields = null! };

        var result = await serviceAsync.IndexDocumentAsync(document);

        document.Fields.ShouldBeEmpty();
        result.IsSuccess.ShouldBeTrue();
    }
    [Fact]
    public async Task IndexDocumentAsync_NullValueInField_Fails()
    {
        var document = new SearchDocument
        {
            Id = "1",
            Type = "TestDocument",
            Index = "test-index-1",
            Fields = new Dictionary<string, IList<object>> { { "Name", ["Jane"] }, { "Tags", ["a", null!] } }
        };

        var result = await serviceAsync.IndexDocumentAsync(document);

        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("Tags");
    }
    [Fact]
    public async Task IndexDocumentAsync_NullField_SendsNothingAndKeepsStreamOpen()
    {
        var observer = new RecordingObserver<SearchDocument>();
        using var subscription = serviceAsync.Subscribe(observer);
        var document = new SearchDocument
        {
            Id = "1",
            Type = "TestDocument",
            Index = "test-index-1",
            Fields = new Dictionary<string, IList<object>> { { "Tags", null! } }
        };

        (await serviceAsync.IndexDocumentAsync(document)).IsFailed.ShouldBeTrue();

        observer.Values.ShouldBeEmpty();
        observer.CompletedCount.ShouldBe(0);
    }
    [Fact]
    public async Task IndexMultipleDocumentsAsync_NullFieldInLaterDocument_IndexesNone()
    {
        var index = $"batch-{Guid.NewGuid():N}";
        (await indexingProviderAsync.CreateIndexAsync(index, Token)).IsSuccess.ShouldBeTrue();
        try
        {
            var observer = new RecordingObserver<SearchDocument>();
            using var subscription = serviceAsync.Subscribe(observer);
            var documents = new List<SearchDocument>
            {
                new() { Id = "1", Type = "TestDocument", Index = index },
                new() { Id = "2", Type = "TestDocument", Index = index },
                new()
                {
                    Id = "3",
                    Type = "TestDocument",
                    Index = index,
                    Fields = new Dictionary<string, IList<object>> { { "Tags", null! } }
                },
            };

            var result = await serviceAsync.IndexMultipleDocumentsAsync(documents, Token);

            result.IsFailed.ShouldBeTrue();
            result.Errors.ShouldHaveSingleItem().Message.ShouldContain("Document 3");
            observer.Values.ShouldBeEmpty();
            observer.CompletedCount.ShouldBe(0);
            // Documents 1 and 2 must not have reached the engine either.
            (await CountDocumentsAsync(index)).ShouldBe(0);
        }
        finally
        {
            await indexingProviderAsync.DeleteIndexAsync(index, Token);
        }
    }
    [Fact]
    public async Task IndexMultipleDocumentsAsync_NullDocument_Fails()
    {
        var documents = new List<SearchDocument> { new() { Id = "1", Type = "TestDocument", Index = "test-index-1" }, null! };

        var result = await serviceAsync.IndexMultipleDocumentsAsync(documents);

        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("document is null");
    }
    [Fact]
    public async Task IndexDocumentsAsyncTest()
    {
        //Arrange
        var documents = new List<SearchDocument>
        {
            new SearchDocument { Id = "1", Type = "TestDocument", Index = "test-index-1", },
            new SearchDocument { Id = "2", Type = "TestDocument", Index = "test-index-1", },
        };

        //Act
        var indexed = await serviceAsync.IndexMultipleDocumentsAsync(documents);

        //Assert
        indexed.IsSuccess.ShouldBeTrue();
        indexed.Value.ShouldBe(documents.Count);
    }
    [Fact]
    public async Task DeleteDocumentAsyncTest()
    {
        //Arrange
        var document = new SearchDocument { Id = "1", Type = "TestDocument", Index = "test-index-1", };
        (await serviceAsync.IndexDocumentAsync(document)).IsSuccess.ShouldBeTrue();

        //Act
        (await serviceAsync.RemoveDocumentAsync(new DeleteDocumentRequest()
        {
            Id = document.Id,
            Type = document.Index,
        })).IsSuccess.ShouldBeTrue();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Counts the documents in <paramref name="index"/>, after whatever refresh the engine needs to see recent changes.</summary>
    protected abstract Task<long> CountDocumentsAsync(string index);

    /// <summary>
    /// Whether removing a document from an index that does not exist succeeds. Lifti keeps indexes in memory, so a
    /// missing index there is expected after a restart and holds no documents; the other engines report a caller mistake.
    /// </summary>
    protected virtual bool RemovingFromMissingIndexSucceeds => false;

    [Fact]
    public async Task RemoveDocumentAsync_MissingDocument_ReturnsSuccess()
    {
        //Arrange
        var index = $"remove-{Guid.NewGuid():N}";
        (await indexingProviderAsync.CreateIndexAsync(index, Token)).IsSuccess.ShouldBeTrue();
        try
        {
            //Act
            var result = await serviceAsync.RemoveDocumentAsync(new DeleteDocumentRequest()
            {
                Id = "never-indexed",
                Type = index,
            }, Token);

            //Assert
            result.IsSuccess.ShouldBeTrue();
        }
        finally
        {
            await indexingProviderAsync.DeleteIndexAsync(index, Token);
        }
    }

    [Fact]
    public async Task RemoveDocumentAsync_AlreadyRemovedDocument_ReturnsSuccess()
    {
        //Arrange
        var index = $"remove-{Guid.NewGuid():N}";
        var document = new SearchDocument { Id = "1", Type = "TestDocument", Index = index, };
        try
        {
            (await serviceAsync.IndexDocumentAsync(document, Token)).IsSuccess.ShouldBeTrue();
            (await CountDocumentsAsync(index)).ShouldBe(1);
            var request = new DeleteDocumentRequest() { Id = document.Id, Type = index, };
            (await serviceAsync.RemoveDocumentAsync(request, Token)).IsSuccess.ShouldBeTrue();

            //Act
            var result = await serviceAsync.RemoveDocumentAsync(request, Token);

            //Assert
            result.IsSuccess.ShouldBeTrue();
            // The document must really be gone, so a provider that succeeds without deleting can't pass.
            (await CountDocumentsAsync(index)).ShouldBe(0);
        }
        finally
        {
            await indexingProviderAsync.DeleteIndexAsync(index, Token);
        }
    }

    [Fact]
    public async Task RemoveDocumentAsync_MissingIndex_DoesNotCreateIndex()
    {
        //Arrange
        var index = $"remove-{Guid.NewGuid():N}";
        try
        {
            //Act
            var result = await serviceAsync.RemoveDocumentAsync(new DeleteDocumentRequest()
            {
                Id = "1",
                Type = index,
            }, Token);

            //Assert
            result.IsSuccess.ShouldBe(RemovingFromMissingIndexSucceeds);
            var exists = await indexingProviderAsync.IndexExistsAsync(index, Token);
            exists.IsSuccess.ShouldBeTrue(string.Join("; ", exists.Errors.Select(error => error.Message)));
            exists.Value.ShouldBeFalse();
        }
        finally
        {
            // Best-effort cleanup: the result is ignored. A failed delete is expected when the index
            // was never created, but it can also be a server error, so it proves nothing about absence.
            // CancellationToken.None, so a cancelled run cannot throw here and hide the assertion failure.
            await indexingProviderAsync.DeleteIndexAsync(index, CancellationToken.None);
        }
    }

    /// <summary>Indexes documents with <paramref name="ids"/> into <paramref name="index"/> and checks they are all there.</summary>
    private async Task IndexIdsAsync(string index, params string[] ids)
    {
        foreach (var id in ids)
        {
            var indexed = await serviceAsync.IndexDocumentAsync(new SearchDocument { Id = id, Type = "TestDocument", Index = index, }, Token);
            indexed.IsSuccess.ShouldBeTrue(string.Join("; ", indexed.Errors.Select(error => error.Message)));
        }

        (await CountDocumentsAsync(index)).ShouldBe(ids.Length);
    }

    [Fact]
    public async Task RemoveDocumentsAsync_SeveralIds_RemovesExactlyThoseAndReturnsTheCount()
    {
        //Arrange
        var index = $"remove-many-{Guid.NewGuid():N}";
        try
        {
            await IndexIdsAsync(index, "1", "2", "3", "4");

            //Act
            var result = await serviceAsync.RemoveDocumentsAsync(index, ["1", "3", "never-indexed"], Token);

            //Assert
            result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
            result.Value.ShouldBe(2);
            (await CountDocumentsAsync(index)).ShouldBe(2);
            // 1 and 3 are gone and 2 and 4 are still there: removing all four again removes only 2 and 4.
            (await serviceAsync.RemoveDocumentsAsync(index, ["1", "2", "3", "4"], Token)).Value.ShouldBe(2);
            (await CountDocumentsAsync(index)).ShouldBe(0);
        }
        finally
        {
            await DeleteIndexQuietlyAsync(index);
        }
    }

    [Fact]
    public async Task RemoveDocumentsAsync_RepeatedId_RemovesAndCountsItOnce()
    {
        //Arrange
        var index = $"remove-many-{Guid.NewGuid():N}";
        try
        {
            await IndexIdsAsync(index, "1", "2");

            //Act
            var result = await serviceAsync.RemoveDocumentsAsync(index, ["1", "1"], Token);

            //Assert
            result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
            result.Value.ShouldBe(1);
            (await CountDocumentsAsync(index)).ShouldBe(1);
        }
        finally
        {
            await DeleteIndexQuietlyAsync(index);
        }
    }

    [Fact]
    public async Task RemoveDocumentsAsync_EmptyList_SucceedsWithZeroAndCallsNothing()
    {
        //Arrange
        var index = $"remove-many-{Guid.NewGuid():N}";
        var observer = new RecordingObserver<DeleteDocumentRequest>();
        using var subscription = serviceAsync.Subscribe(observer);
        try
        {
            //Act
            var serviceResult = await serviceAsync.RemoveDocumentsAsync(index, [], Token);
            var providerResult = await indexingProviderAsync.RemoveDocumentsAsync(index, [], Token);

            //Assert
            serviceResult.IsSuccess.ShouldBeTrue();
            serviceResult.Value.ShouldBe(0);
            providerResult.IsSuccess.ShouldBeTrue();
            providerResult.Value.ShouldBe(0);
            observer.Values.ShouldBeEmpty();
            // The index does not exist, so a provider that reached the engine would have failed or created it.
            await IndexShouldExistAsync(index, false);
        }
        finally
        {
            await DeleteIndexQuietlyAsync(index);
        }
    }

    [Fact]
    public async Task RemoveDocumentsAsync_NullIds_Fails()
    {
        var serviceResult = await serviceAsync.RemoveDocumentsAsync("any-index", null!, Token);
        var providerResult = await indexingProviderAsync.RemoveDocumentsAsync("any-index", null!, Token);

        serviceResult.Errors.ShouldHaveSingleItem().Message.ShouldContain("ids to remove are null");
        providerResult.Errors.ShouldHaveSingleItem().Message.ShouldContain("ids to remove are null");
    }

    [Fact]
    public async Task RemoveDocumentsAsync_NullOrEmptyId_FailsAndRemovesNothing()
    {
        //Arrange
        var index = $"remove-many-{Guid.NewGuid():N}";
        try
        {
            await IndexIdsAsync(index, "1", "2");

            //Act
            var serviceResult = await serviceAsync.RemoveDocumentsAsync(index, ["1", null!], Token);
            var providerResult = await indexingProviderAsync.RemoveDocumentsAsync(index, ["2", ""], Token);

            //Assert
            serviceResult.Errors.ShouldHaveSingleItem().Message.ShouldContain("null or empty id");
            providerResult.Errors.ShouldHaveSingleItem().Message.ShouldContain("null or empty id");
            (await CountDocumentsAsync(index)).ShouldBe(2);
        }
        finally
        {
            await DeleteIndexQuietlyAsync(index);
        }
    }

    [Fact]
    public async Task RemoveDocumentsAsync_MissingIndex_FailsNamingTheIdsOrSucceedsWithZero()
    {
        //Arrange
        var index = $"remove-many-{Guid.NewGuid():N}";
        var observer = new RecordingObserver<DeleteDocumentRequest>();
        using var subscription = serviceAsync.Subscribe(observer);
        try
        {
            //Act
            var result = await serviceAsync.RemoveDocumentsAsync(index, ["first-id", "second-id"], Token);

            //Assert
            if (RemovingFromMissingIndexSucceeds)
            {
                result.IsSuccess.ShouldBeTrue();
                result.Value.ShouldBe(0);
                observer.Values.Count.ShouldBe(2);
            }
            else
            {
                var message = result.Errors.ShouldHaveSingleItem().Message;
                message.ShouldContain("failed for ids first-id, second-id");
                message.ShouldContain("0 of 2 documents were removed");
                observer.Values.ShouldBeEmpty();
            }

            await IndexShouldExistAsync(index, false);
        }
        finally
        {
            await DeleteIndexQuietlyAsync(index);
        }
    }

    [Fact]
    public async Task RemoveDocumentsAsync_Subscribed_SendsOnNextForEachDistinctId()
    {
        //Arrange
        var index = $"remove-many-{Guid.NewGuid():N}";
        var observer = new RecordingObserver<DeleteDocumentRequest>();
        try
        {
            await IndexIdsAsync(index, "1", "2");
            using var subscription = serviceAsync.Subscribe(observer);

            //Act
            (await serviceAsync.RemoveDocumentsAsync(index, ["1", "never-indexed", "1"], Token)).IsSuccess.ShouldBeTrue();

            //Assert
            observer.Values.Select(request => (request.Id, request.Type)).ShouldBe([("1", index), ("never-indexed", index)]);
            observer.CompletedCount.ShouldBe(0);
            observer.Errors.ShouldBeEmpty();
        }
        finally
        {
            await DeleteIndexQuietlyAsync(index);
        }
    }

    [Fact]
    public async Task IndexDocumentAsync_Subscribed_SendsOnNextForEachIndexedDocumentAndKeepsStreamOpen()
    {
        //Arrange
        var index = $"observe-{Guid.NewGuid():N}";
        var first = new SearchDocument { Id = "1", Type = "TestDocument", Index = index, };
        var second = new SearchDocument { Id = "2", Type = "TestDocument", Index = index, };
        var observer = new RecordingObserver<SearchDocument>();
        using var subscription = serviceAsync.Subscribe(observer);
        try
        {
            //Act
            (await serviceAsync.IndexDocumentAsync(first, Token)).IsSuccess.ShouldBeTrue();
            (await serviceAsync.IndexDocumentAsync(second, Token)).IsSuccess.ShouldBeTrue();

            //Assert
            observer.Values.ShouldBe([first, second]);
            observer.CompletedCount.ShouldBe(0);
            observer.Errors.ShouldBeEmpty();
        }
        finally
        {
            await indexingProviderAsync.DeleteIndexAsync(index, Token);
        }
    }

    [Fact]
    public async Task IndexDocumentAsync_AfterUnsubscribe_SendsNothing()
    {
        //Arrange
        var index = $"observe-{Guid.NewGuid():N}";
        var observer = new RecordingObserver<SearchDocument>();
        serviceAsync.Subscribe(observer).Dispose();
        try
        {
            //Act
            (await serviceAsync.IndexDocumentAsync(new SearchDocument { Id = "1", Type = "TestDocument", Index = index, }, Token))
                .IsSuccess.ShouldBeTrue();

            //Assert
            observer.Values.ShouldBeEmpty();
            observer.CompletedCount.ShouldBe(0);
        }
        finally
        {
            await indexingProviderAsync.DeleteIndexAsync(index, Token);
        }
    }

    [Fact]
    public async Task RemoveDocumentAsync_Subscribed_SendsOnNextForEachRemovalAndKeepsStreamOpen()
    {
        //Arrange
        var index = $"observe-{Guid.NewGuid():N}";
        var observer = new RecordingObserver<DeleteDocumentRequest>();
        try
        {
            (await serviceAsync.IndexDocumentAsync(new SearchDocument { Id = "1", Type = "TestDocument", Index = index, }, Token))
                .IsSuccess.ShouldBeTrue();
            using var subscription = serviceAsync.Subscribe(observer);
            var first = new DeleteDocumentRequest() { Id = "1", Type = index, };
            var second = new DeleteDocumentRequest() { Id = "1", Type = index, };

            //Act
            (await serviceAsync.RemoveDocumentAsync(first, Token)).IsSuccess.ShouldBeTrue();
            (await serviceAsync.RemoveDocumentAsync(second, Token)).IsSuccess.ShouldBeTrue();

            //Assert
            observer.Values.ShouldBe([first, second]);
            observer.CompletedCount.ShouldBe(0);
            observer.Errors.ShouldBeEmpty();
        }
        finally
        {
            await indexingProviderAsync.DeleteIndexAsync(index, Token);
        }
    }

    [Fact]
    public async Task RemoveDocumentAsync_MissingIndex_SendsOnNextOnlyWhenRemovalSucceeds()
    {
        //Arrange
        var observer = new RecordingObserver<DeleteDocumentRequest>();
        using var subscription = serviceAsync.Subscribe(observer);
        var request = new DeleteDocumentRequest() { Id = "1", Type = $"remove-{Guid.NewGuid():N}", };

        //Act
        var result = await serviceAsync.RemoveDocumentAsync(request, Token);

        //Assert
        result.IsSuccess.ShouldBe(RemovingFromMissingIndexSucceeds);
        observer.Values.Count.ShouldBe(RemovingFromMissingIndexSucceeds ? 1 : 0);
        observer.CompletedCount.ShouldBe(0);
        observer.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveAllDocumentsAsync_IndexWithDocuments_ReturnsTheRemovedCount()
    {
        //Arrange
        var index = $"remove-all-{Guid.NewGuid():N}";
        try
        {
            await IndexIdsAsync(index, "1", "2", "3");

            //Act
            var result = await serviceAsync.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = index, }, Token);

            //Assert
            result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
            result.Value.ShouldBe(3);
            (await CountDocumentsAsync(index)).ShouldBe(0);
        }
        finally
        {
            await DeleteIndexQuietlyAsync(index);
        }
    }

    [Fact]
    public async Task RemoveAllDocumentsAsync_Subscribed_SendsOnNextAndKeepsStreamOpen()
    {
        //Arrange
        var index = $"observe-{Guid.NewGuid():N}";
        var observer = new RecordingObserver<DeleteAllDocumentsRequest>();
        try
        {
            (await serviceAsync.IndexDocumentAsync(new SearchDocument { Id = "1", Type = "TestDocument", Index = index, }, Token))
                .IsSuccess.ShouldBeTrue();
            using var subscription = serviceAsync.Subscribe(observer);
            var request = new DeleteAllDocumentsRequest() { IndexName = index, };

            //Act
            (await serviceAsync.RemoveAllDocumentsAsync(request, Token)).IsSuccess.ShouldBeTrue();

            //Assert
            observer.Values.ShouldBe([request]);
            observer.CompletedCount.ShouldBe(0);
            observer.Errors.ShouldBeEmpty();
        }
        finally
        {
            await indexingProviderAsync.DeleteIndexAsync(index, Token);
        }
    }
}
