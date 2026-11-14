using Bielu.BetterSearch.ElasticSearch.Services;
using Bielu.BetterSearch.Services;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.ElasticSearch.Configuration;

/// <summary>
/// Checks the connection settings, limits, field mappings and bulk batch sizes, at startup and whenever the options
/// are read, listing every setting that is wrong and naming each one. The connection settings are checked as each call
/// checks them, after merging each <c>Indexes</c> entry with the shared settings.
/// </summary>
internal sealed class ElasticSearchOptionsValidator : IValidateOptions<ElasticSearchOptions>
{
    public ValidateOptionsResult Validate(string? name, ElasticSearchOptions options)
    {
        var key = ElasticSearchOptions.ElasticSearchOptionsKey;
        var failures = ConnectionFailures(options)
            .Concat(new[] { ConnectionSettingsChecks.GreaterThanZeroFailure($"{key}:{nameof(ElasticSearchOptions.MaxResultWindow)}", options.MaxResultWindow) }.OfType<string>())
            .Concat(FieldMappings.Validate($"{key}:{nameof(ElasticSearchOptions.SharedIndexSettings)}", options.SharedIndexSettings?.Mappings?.ToList<ElasticSearchFieldMapping?>()))
            .Concat((options.Indexes ?? []).SelectMany(index =>
                FieldMappings.Validate($"{key}:{nameof(ElasticSearchOptions.Indexes)}:{index.Key}", index.Value?.Mappings?.ToList<ElasticSearchFieldMapping?>())))
            .Concat(BulkBatchSizeFailures($"{key}:{nameof(ElasticSearchOptions.SharedIndexSettings)}", options.SharedIndexSettings))
            .Concat((options.Indexes ?? []).SelectMany(index =>
                BulkBatchSizeFailures($"{key}:{nameof(ElasticSearchOptions.Indexes)}:{index.Key}", index.Value)))
            .ToList();
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>
    /// Options with no connection string, authentication or <c>Indexes</c> entry at all fail once, naming the section.
    /// Otherwise the shared settings are checked as an index not listed under <c>Indexes</c> gets them, unless they set no
    /// connection and every index is listed with its own. An <c>Indexes</c> entry is checked after merging, unless it
    /// sets no connection of its own and so has the shared one, which is checked already.
    /// </summary>
    private static IEnumerable<string> ConnectionFailures(ElasticSearchOptions options)
    {
        var shared = options.SharedIndexSettings ?? new ElasticSearchIndexSettings();
        var indexes = options.Indexes ?? [];
        var sharedConnection = HasConnection(shared);
        if (string.IsNullOrEmpty(shared.ConnectionString) && shared.AuthenticationType is null && indexes.Count == 0)
        {
            return [ConnectionSettingsChecks.NoSettingsFailure(ElasticSearchOptions.ElasticSearchOptionsKey)];
        }

        var sharedFailures = sharedConnection || indexes.Count == 0 || indexes.Values.Any(index => index is null || !HasConnection(index))
            ? ElasticSearchClientSettingsManager.ConnectionFailures(ElasticSearchClientSettingsManager.MergeSettings(new ElasticSearchIndexSettings(), shared), null)
            : [];
        return sharedFailures.Concat(indexes
            .Where(index => index.Value is not null && HasConnection(index.Value))
            .SelectMany(index => ElasticSearchClientSettingsManager.ConnectionFailures(
                ElasticSearchClientSettingsManager.MergeSettings(index.Value, shared), index.Key)));
    }

    private static bool HasConnection(ElasticSearchIndexSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.ConnectionString) || settings.AuthenticationType is not null;

    private static IEnumerable<string> BulkBatchSizeFailures(string path, ElasticSearchIndexSettings? settings) =>
        settings?.BulkBatchSize is <= 0
            ? [$"{path}:{nameof(ElasticSearchIndexSettings.BulkBatchSize)} is {settings.BulkBatchSize}, but it must be greater than 0."]
            : [];
}
