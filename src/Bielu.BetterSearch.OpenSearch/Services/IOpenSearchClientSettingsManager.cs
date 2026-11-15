using Bielu.BetterSearch.OpenSearch.Configuration;
using OpenSearch.Client;
using OpenSearch.Net;

namespace Bielu.BetterSearch.OpenSearch.Services;

internal interface IOpenSearchClientSettingsManager
{
    /// <summary>The settings an index connects with: its own, with what it leaves out taken from the shared ones.</summary>
    OpenSearchIndexSettings GetConnectionSettings(string indexName);

    /// <summary>
    /// Builds client settings that send requests through <paramref name="connection"/>, or the client's default
    /// connection when it is <c>null</c>, throwing <see cref="ArgumentException"/> when the settings are invalid.
    /// </summary>
    ConnectionSettings CreateClientSettings(OpenSearchIndexSettings settings, IConnection? connection = null);

    /// <summary>
    /// The name OpenSearch knows <paramref name="indexName"/> by, which every request uses: the configured
    /// <see cref="OpenSearchOptions.IndexPrefix"/> followed by the name.
    /// </summary>
    string GetIndexName(string indexName);
}
