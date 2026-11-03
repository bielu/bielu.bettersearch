using OpenSearch.Client;

namespace Bielu.BetterSearch.OpenSearch.Services;

internal interface IOpenSearchClientSettingsManager
{
    ConnectionSettings GetOrCreateClientSettings(string indexName);
}
