using Bielu.BetterSearch.Services;
using Bielu.BetterSearch.Typesense.Configuration;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.Typesense.Services;

internal sealed class TypesenseClientSettingsManager(IOptionsMonitor<TypesenseOptions> optionsMonitor)
    : ITypesenseClientSettingsManager
{
    public TypesenseIndexSettings GetOrCreateClientSettings(string indexName)
    {
        var options = optionsMonitor.CurrentValue;
        var shared = options.SharedIndexSettings;
        var settings = options.Indexes != null && options.Indexes.TryGetValue(indexName, out var indexSpecific)
            ? MergeSettings(indexSpecific, shared)
            : shared;

        // Checked here so a URL without a scheme fails with a message that names the setting.
        ConnectionSettingsChecks.ThrowIfAny(ConnectionFailures(settings, SettingSource.Unknown));
        return settings;
    }

    /// <summary>
    /// The connection setting failures of <paramref name="settings"/>, already merged, each naming the setting to fix.
    /// The options validator runs the same checks at startup. The API key is optional, as a node can run without one
    /// in development; each node of <c>AdditionalNodes</c> is checked when a client is created.
    /// </summary>
    internal static IEnumerable<string> ConnectionFailures(TypesenseIndexSettings settings, SettingSource source) =>
        new[] { ConnectionSettingsChecks.HttpUrlFailure(TypesenseOptions.TypesenseOptionsKey, source, settings.ConnectionString) }
            .OfType<string>();

    internal static TypesenseIndexSettings MergeSettings(TypesenseIndexSettings indexSpecific, TypesenseIndexSettings shared)
    {
        if (!string.IsNullOrWhiteSpace(indexSpecific.ConnectionString))
        {
            return indexSpecific;
        }

        // Merge into a copy: the monitored options are shared, so mutating them would let a concurrent
        // lookup see the inherited nodes before the inherited key.
        return new TypesenseIndexSettings
        {
            IndexName = indexSpecific.IndexName,
            AliasName = indexSpecific.AliasName,
            TypeName = indexSpecific.TypeName,
            IndexingEnabled = indexSpecific.IndexingEnabled,
            DevMode = indexSpecific.DevMode,
            ConnectionString = shared.ConnectionString,
            AdditionalNodes = indexSpecific.AdditionalNodes ?? shared.AdditionalNodes,
            ApiKey = indexSpecific.ApiKey ?? shared.ApiKey,
        };
    }
}
