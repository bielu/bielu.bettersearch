﻿using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Lifti.Extensions;
using FluentResults;
using Lifti;

namespace Bielu.BetterSearch.Lifti.Services;

public class LiftiIndexingProviderAsync(IClientFactoryAsync<IFullTextIndex<string>> clientFactory) : IIndexingProviderAsync
{
    public async Task IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) => (await clientFactory.GetOrCreateClientAsync(document.Index)).AddAsync(document,cancellationToken);
    Task<Result<int>> IIndexingProviderAsync.IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document, CancellationToken cancellationToken) => throw new NotImplementedException();

    Task<Result> IIndexingProviderAsync.RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken) => throw new NotImplementedException();

    Task<Result<int>> IIndexingProviderAsync.RemoveAllDocumentsAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<Result<bool>> EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<Result<bool>> DeleteIndexAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<Result<bool>> IndexExistsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default) => throw new NotImplementedException();


    Task<Result> IIndexingProviderAsync.IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken) => throw new NotImplementedException();

    public async Task IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public async Task RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public async Task RemoveAllDocumentsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
