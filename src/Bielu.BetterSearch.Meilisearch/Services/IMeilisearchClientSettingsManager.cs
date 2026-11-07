using Bielu.BetterSearch.Meilisearch.Configuration;

namespace Bielu.BetterSearch.Meilisearch.Services;

internal interface IMeilisearchClientSettingsManager
{
    MeilisearchIndexSettings GetOrCreateClientSettings(string indexName);
}
