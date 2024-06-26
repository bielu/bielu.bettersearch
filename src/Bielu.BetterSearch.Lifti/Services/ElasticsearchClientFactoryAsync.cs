﻿using Bielu.BetterSearch.Abstractions.Services;
using Lifti;

namespace Bielu.BetterSearch.Lifti.Services;

public class ElasticsearchClientFactoryAsync(ILiftiIndexManager indexManager)
    : IClientFactoryAsync<IFullTextIndex<string>>
{
    public Task<IFullTextIndex<string>> GetOrCreateClientAsync(string indexName) =>
        Task.FromResult(indexManager.GetOrCreateIndexAsync("default"));
}
