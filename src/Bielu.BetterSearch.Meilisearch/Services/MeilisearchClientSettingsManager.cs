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

        // Merge into a copy: the monitored options are shared, so mutating them would let a concurrent
        // lookup see the inherited URL before the inherited key.
        return new MeilisearchIndexSettings
        {
            IndexName = indexSpecific.IndexName,
            AliasName = indexSpecific.AliasName,
            TypeName = indexSpecific.TypeName,
            AliasingEnabled = indexSpecific.AliasingEnabled,
            IndexingEnabled = indexSpecific.IndexingEnabled,
            DevMode = indexSpecific.DevMode,
            ConnectionString = shared.ConnectionString,
            ApiKey = indexSpecific.ApiKey ?? shared.ApiKey,
        };
    }
}
