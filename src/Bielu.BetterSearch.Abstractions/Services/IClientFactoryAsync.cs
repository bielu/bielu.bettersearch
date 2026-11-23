namespace Bielu.BetterSearch.Abstractions.Services;

/// <summary>Gives a provider the engine client for an index.</summary>
/// <typeparam name="TClient">The engine's client type.</typeparam>
public interface IClientFactoryAsync<TClient>
{
    /// <summary>The client for the connection settings of <paramref name="indexName"/>, created when there is none yet.</summary>
    /// <param name="indexName">The index the client is for.</param>
    /// <returns>The client.</returns>
    Task<TClient> GetOrCreateClientAsync(string indexName);
}
