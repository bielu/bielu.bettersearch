using Bielu.BetterSearch.OpenSearch.Services;
using Bielu.BetterSearch.Services;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.OpenSearch.Configuration;

/// <summary>
/// Checks the connection settings, limits, field mappings, bulk batch sizes and the index prefix, at startup and whenever the options
/// are read, listing every setting that is wrong and naming each one. The connection settings are checked as each call
/// checks them, after merging each <c>Indexes</c> entry with the shared settings.
/// </summary>
internal sealed class OpenSearchOptionsValidator : IValidateOptions<OpenSearchOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenSearchOptions options)
    {
        var key = OpenSearchOptions.OpenSearchOptionsKey;
        var failures = ConnectionFailures(options)
            .Concat(new[] { ConnectionSettingsChecks.GreaterThanZeroFailure($"{key}:{nameof(OpenSearchOptions.MaxResultWindow)}", options.MaxResultWindow) }.OfType<string>())
            .Concat(FieldMappings.Validate($"{key}:{nameof(OpenSearchOptions.SharedIndexSettings)}", options.SharedIndexSettings?.Mappings?.ToList<OpenSearchFieldMapping?>()))
            .Concat((options.Indexes ?? []).SelectMany(index =>
                FieldMappings.Validate($"{key}:{nameof(OpenSearchOptions.Indexes)}:{index.Key}", index.Value?.Mappings?.ToList<OpenSearchFieldMapping?>())))
            .Concat(BulkBatchSizeFailures($"{key}:{nameof(OpenSearchOptions.SharedIndexSettings)}", options.SharedIndexSettings))
            .Concat(ClientSettingsFailures($"{key}:{nameof(OpenSearchOptions.SharedIndexSettings)}", options.SharedIndexSettings))
            .Concat((options.Indexes ?? []).SelectMany(index =>
                ClientSettingsFailures($"{key}:{nameof(OpenSearchOptions.Indexes)}:{index.Key}", index.Value)))
            .Concat((options.Indexes ?? []).SelectMany(index =>
                BulkBatchSizeFailures($"{key}:{nameof(OpenSearchOptions.Indexes)}:{index.Key}", index.Value)))
            .Concat(IndexNames.ValidatePrefix($"{key}:{nameof(OpenSearchOptions.IndexPrefix)}", options.IndexPrefix))
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
    private static IEnumerable<string> ConnectionFailures(OpenSearchOptions options)
    {
        var shared = options.SharedIndexSettings ?? new OpenSearchIndexSettings();
        var indexes = options.Indexes ?? [];
        var sharedConnection = !string.IsNullOrWhiteSpace(shared.ConnectionString);
        if (string.IsNullOrEmpty(shared.ConnectionString) && shared.AuthenticationType is null && indexes.Count == 0)
        {
            return [ConnectionSettingsChecks.NoSettingsFailure(OpenSearchOptions.OpenSearchOptionsKey)];
        }

        var sharedFailures = sharedConnection || indexes.Count == 0 || indexes.Values.Any(index => index is null || !HasConnection(index))
            ? OpenSearchClientSettingsManager.ConnectionFailures(OpenSearchClientSettingsManager.MergeSettings(new OpenSearchIndexSettings(), shared),
                SettingSource.Unknown, SettingSource.Unknown)
            : [];
        return sharedFailures.Concat(indexes
            .Where(index => index.Value is not null && HasConnection(index.Value))
            .SelectMany(index => OpenSearchClientSettingsManager.ConnectionFailures(
                OpenSearchClientSettingsManager.MergeSettings(index.Value, shared),
                new SettingSource(index.Key, Inherited: string.IsNullOrWhiteSpace(index.Value.ConnectionString)),
                new SettingSource(index.Key, Inherited: index.Value.AuthenticationType is null))));
    }

    private static bool HasConnection(OpenSearchIndexSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.ConnectionString) || settings.AuthenticationType is not null;

    private static IEnumerable<string> BulkBatchSizeFailures(string path, OpenSearchIndexSettings? settings) =>
        settings?.BulkBatchSize is <= 0
            ? [$"{path}:{nameof(OpenSearchIndexSettings.BulkBatchSize)} is {settings.BulkBatchSize}, but it must be greater than 0."]
            : [];

    private static IEnumerable<string> ClientSettingsFailures(string path, OpenSearchIndexSettings? settings) =>
        new[]
        {
            settings?.RequestTimeout is { } timeout
                ? ConnectionSettingsChecks.RequestTimeoutFailure($"{path}:{nameof(OpenSearchIndexSettings.RequestTimeout)}", timeout)
                : null,
            settings?.MaxRetries is { } retries
                ? ConnectionSettingsChecks.NotNegativeFailure($"{path}:{nameof(OpenSearchIndexSettings.MaxRetries)}", retries)
                : null,
        }.OfType<string>();
}
