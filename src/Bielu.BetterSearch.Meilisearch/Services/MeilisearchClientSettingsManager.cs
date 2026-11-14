using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Services;
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

        // Checked here so a URL without a scheme fails with a message that names the setting.
        ConnectionSettingsChecks.ThrowIfAny(ConnectionFailures(settings, null));
        return settings;
    }

    /// <summary>
    /// The connection setting failures of <paramref name="settings"/>, already merged, each naming the setting to fix.
    /// The options validator runs the same checks at startup. The API key is optional, as a server can run without one.
    /// </summary>
    /// <remarks><c>index</c> is the <c>Indexes</c> entry the settings come from, or <c>null</c> when that is not known or they are the shared settings.</remarks>
    internal static IEnumerable<string> ConnectionFailures(MeilisearchIndexSettings settings, string? index) =>
        new[] { ConnectionSettingsChecks.HttpUrlFailure(MeilisearchOptions.MeilisearchOptionsKey, index, settings.ConnectionString) }
            .OfType<string>();

    internal static MeilisearchIndexSettings MergeSettings(MeilisearchIndexSettings indexSpecific,
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
            IndexingEnabled = indexSpecific.IndexingEnabled,
            DevMode = indexSpecific.DevMode,
            ConnectionString = shared.ConnectionString,
            ApiKey = indexSpecific.ApiKey ?? shared.ApiKey,
            BulkBatchSize = indexSpecific.BulkBatchSize ?? shared.BulkBatchSize,
        };
    }
}
