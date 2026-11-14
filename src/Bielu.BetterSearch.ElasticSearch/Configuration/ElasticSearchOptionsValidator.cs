using Bielu.BetterSearch.ElasticSearch.Services;
using Bielu.BetterSearch.Services;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.ElasticSearch.Configuration;

/// <summary>
/// Checks the connection and TLS settings, limits, field mappings, bulk batch sizes and the index prefix, at startup and whenever the options
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
            .Concat(IndexNames.ValidatePrefix($"{key}:{nameof(ElasticSearchOptions.IndexPrefix)}", options.IndexPrefix))
            .Concat((options.Indexes ?? []).Where(index => index.Value is not null && !ElasticSearchClientSettingsManager.HasOwnEndpoint(index.Value)).Select(index =>
                ConnectionSettingsChecks.TlsWithoutConnectionStringFailure(key, index.Key, index.Value.ConnectionString,
                    index.Value.CertificateFingerprint, index.Value.DevelopmentOnlySkipCertificateValidation)).OfType<string>())
            .ToList();
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>
    /// Options with no connection string, authentication or <c>Indexes</c> entry at all fail once, naming the section.
    /// Otherwise the shared settings are checked as an index not listed under <c>Indexes</c> gets them, unless they set no
    /// connection string and every index is listed with its own connection: shared authentication is then a default the
    /// indexes inherit, checked with each of them. An <c>Indexes</c> entry is checked after merging, unless it sets no
    /// connection of its own and so has the shared one, which is checked already.
    /// </summary>
    private static IEnumerable<string> ConnectionFailures(ElasticSearchOptions options)
    {
        var shared = options.SharedIndexSettings ?? new ElasticSearchIndexSettings();
        var indexes = options.Indexes ?? [];
        var sharedConnection = !string.IsNullOrWhiteSpace(shared.ConnectionString);
        if (string.IsNullOrEmpty(shared.ConnectionString) && shared.AuthenticationType is null && indexes.Count == 0)
        {
            return [ConnectionSettingsChecks.NoSettingsFailure(ElasticSearchOptions.ElasticSearchOptionsKey)];
        }

        var sharedFailures = sharedConnection || indexes.Count == 0 || indexes.Values.Any(index => index is null || !HasConnection(index))
            ? ElasticSearchClientSettingsManager.ConnectionFailures(ElasticSearchClientSettingsManager.MergeSettings(new ElasticSearchIndexSettings(), shared),
                SettingSource.Unknown, SettingSource.Unknown)
            : [];
        return sharedFailures.Concat(indexes
            .Where(index => index.Value is not null && HasConnection(index.Value))
            .SelectMany(index => ElasticSearchClientSettingsManager.ConnectionFailures(
                ElasticSearchClientSettingsManager.MergeSettings(index.Value, shared),
                new SettingSource(index.Key, Inherited: string.IsNullOrWhiteSpace(index.Value.ConnectionString)),
                new SettingSource(index.Key, Inherited: index.Value.AuthenticationType is null))));
    }

    private static bool HasConnection(ElasticSearchIndexSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.ConnectionString) || settings.AuthenticationType is not null;

    private static IEnumerable<string> BulkBatchSizeFailures(string path, ElasticSearchIndexSettings? settings) =>
        settings?.BulkBatchSize is <= 0
            ? [$"{path}:{nameof(ElasticSearchIndexSettings.BulkBatchSize)} is {settings.BulkBatchSize}, but it must be greater than 0."]
            : [];
}
