using Bielu.BetterSearch.OpenSearch.Configuration;
using OpenSearch.Client;

namespace Bielu.BetterSearch.OpenSearch.Services;

internal interface IOpenSearchClientSettingsManager
{
    /// <summary>The settings an index connects with: its own, with what it leaves out taken from the shared ones.</summary>
    OpenSearchIndexSettings GetConnectionSettings(string indexName);

    /// <summary>Builds client settings, throwing <see cref="ArgumentException"/> when the settings are invalid.</summary>
    ConnectionSettings CreateClientSettings(OpenSearchIndexSettings settings);

    /// <summary>
    /// The name OpenSearch knows <paramref name="indexName"/> by, which every request uses: the configured
    /// <see cref="OpenSearchOptions.IndexPrefix"/> followed by the name.
    /// </summary>
    string GetIndexName(string indexName);
}
