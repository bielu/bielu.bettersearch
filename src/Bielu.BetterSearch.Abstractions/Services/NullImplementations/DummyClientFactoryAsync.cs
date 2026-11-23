namespace Bielu.BetterSearch.Abstractions.Services.NullImplementations;

/// <summary>The client factory used when no provider registered one. It throws, saying so.</summary>
/// <typeparam name="TClient">The engine's client type.</typeparam>
public class DummyClientFactoryAsync<TClient> : IClientFactoryAsync<TClient>
{
    // IClientFactoryAsync has no Result to fail with, so this throws with a message that says what is missing.
    /// <inheritdoc />
    public Task<TClient> GetOrCreateClientAsync(string indexName) =>
        throw new InvalidOperationException(
            $"No client factory is registered for {typeof(TClient).Name}. Call the provider extension inside AddBetterSearch.");
}
