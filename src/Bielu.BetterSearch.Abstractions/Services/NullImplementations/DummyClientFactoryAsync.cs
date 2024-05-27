namespace Bielu.BetterSearch.Abstractions.Services.NullImplementations;

public class DummyClientFactoryAsync<TClient> : IClientFactoryAsync<TClient>
{
    public Task<TClient> GetOrCreateClientAsync(string indexName)
    {
        // Implement your logic here
        throw new NotImplementedException();
    }
}
