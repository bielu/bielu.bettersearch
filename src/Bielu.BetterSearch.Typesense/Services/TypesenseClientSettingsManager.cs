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
        return options.Indexes != null && options.Indexes.TryGetValue(indexName, out var indexSpecific)
            ? MergeSettings(indexSpecific, shared)
            : shared;
    }

    private static TypesenseIndexSettings MergeSettings(TypesenseIndexSettings indexSpecific, TypesenseIndexSettings shared)
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
            AliasingEnabled = indexSpecific.AliasingEnabled,
            IndexingEnabled = indexSpecific.IndexingEnabled,
            DevMode = indexSpecific.DevMode,
            ConnectionString = shared.ConnectionString,
            AdditionalNodes = indexSpecific.AdditionalNodes ?? shared.AdditionalNodes,
            ApiKey = indexSpecific.ApiKey ?? shared.ApiKey,
        };
    }
}
