namespace Bielu.BetterSearch.Abstractions.Services.NullImplementations;

public class DummyClientFactoryAsync<TClient> : IClientFactoryAsync<TClient>
{
    // IClientFactoryAsync has no Result to fail with, so this throws with a message that says what is missing.
    public Task<TClient> GetOrCreateClientAsync(string indexName) =>
        throw new InvalidOperationException(
            $"No client factory is registered for {typeof(TClient).Name}. Call the provider extension inside AddBetterSearch.");
}
