using Bielu.BetterSearch.Meilisearch.Configuration;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.Meilisearch.Services;

internal sealed class MeilisearchClientSettingsManager(IOptionsMonitor<MeilisearchOptions> optionsMonitor)
    : IMeilisearchClientSettingsManager
{
    public MeilisearchIndexSettings GetOrCreateClientSettings(string indexName)
    {
        var options = optionsMonitor.CurrentValue;
        var shared = options.SharedIndexSettings;
        var settings = options.Indexes != null && options.Indexes.TryGetValue(indexName, out var indexSpecific)
            ? MergeSettings(indexSpecific, shared)
            : shared;

        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            throw new ArgumentException("Meilisearch connection string is missing");
        }

        return settings;
    }

    private static MeilisearchIndexSettings MergeSettings(MeilisearchIndexSettings indexSpecific,
        MeilisearchIndexSettings shared)
    {
        if (!string.IsNullOrWhiteSpace(indexSpecific.ConnectionString))
        {
            return indexSpecific;
        }

        indexSpecific.ConnectionString = shared.ConnectionString;
        indexSpecific.ApiKey ??= shared.ApiKey;
        return indexSpecific;
    }
}
