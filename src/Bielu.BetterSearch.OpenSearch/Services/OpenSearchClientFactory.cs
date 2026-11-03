using Bielu.BetterSearch.Abstractions.Services;
using OpenSearch.Client;

namespace Bielu.BetterSearch.OpenSearch.Services;

internal sealed class OpenSearchClientFactory(IOpenSearchClientSettingsManager settingsManager) : IClientFactoryAsync<IOpenSearchClient>
{
    public Task<IOpenSearchClient> GetOrCreateClientAsync(string indexName) =>
        Task.FromResult<IOpenSearchClient>(new OpenSearchClient(settingsManager.GetOrCreateClientSettings(indexName)));
}
