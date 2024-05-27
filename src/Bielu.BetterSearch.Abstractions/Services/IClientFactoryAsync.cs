namespace Bielu.BetterSearch.Abstractions.Services;

public interface IClientFactoryAsync<TClient>
{
    Task<TClient> GetOrCreateClientAsync(string indexName);
}
