using FluentResults;
using Lifti;

namespace Bielu.BetterSearch.Lifti.Services;

/// <summary>Holds the Lifti provider's in-memory full-text indexes, by name. Names are compared case-sensitively.</summary>
public interface ILiftiIndexManager
{
    /// <summary>Returns the index named <paramref name="key"/>, creating an empty one when it does not exist.</summary>
    /// <param name="key">The index name.</param>
    /// <returns>The index.</returns>
    public Task<Result<IFullTextIndex<string>>> GetOrCreateIndexAsync(string key);
    /// <summary>Creates an empty index named <paramref name="key"/>, replacing any index already held under that name.</summary>
    /// <param name="key">The index name.</param>
    /// <returns>The new index.</returns>
    public Task<Result<IFullTextIndex<string>>> CreateIndexAsync(string key);
    /// <summary>Returns the index, or <c>null</c> when it does not exist, without creating it.</summary>
    Task<Result<IFullTextIndex<string>?>> GetIndexAsync(string key);
    /// <summary>Whether an index named <paramref name="name"/> exists.</summary>
    /// <param name="name">The index name.</param>
    /// <returns><c>true</c> when the index exists.</returns>
    Task<Result<bool>> ExistsAsync(string name);
    /// <summary>Removes the index named <paramref name="name"/> and its documents.</summary>
    /// <param name="name">The index name.</param>
    /// <returns><c>true</c> when an index was removed, <c>false</c> when none existed.</returns>
    Task<Result<bool>> DeleteIndexAsync(string name);
}
