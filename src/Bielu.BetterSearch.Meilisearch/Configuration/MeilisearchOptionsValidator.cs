using Bielu.BetterSearch.Meilisearch.Services;
using Bielu.BetterSearch.Services;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.Meilisearch.Configuration;

/// <summary>
/// Checks the connection settings, limits, intervals, bulk batch sizes and field names, at startup and whenever the
/// options are read, listing every setting that is wrong and naming each one. The connection settings are checked as
/// each call checks them, after merging each <c>Indexes</c> entry with the shared settings.
/// </summary>
internal sealed class MeilisearchOptionsValidator : IValidateOptions<MeilisearchOptions>
{
    public ValidateOptionsResult Validate(string? name, MeilisearchOptions options)
    {
        var key = MeilisearchOptions.MeilisearchOptionsKey;
        var failures = ConnectionFailures(options)
            .Concat(new[]
            {
                ConnectionSettingsChecks.GreaterThanZeroFailure($"{key}:{nameof(MeilisearchOptions.MaxTotalHits)}", options.MaxTotalHits),
                ConnectionSettingsChecks.NotNegativeFailure($"{key}:{nameof(MeilisearchOptions.TaskTimeout)}", options.TaskTimeout),
                ConnectionSettingsChecks.NotNegativeFailure($"{key}:{nameof(MeilisearchOptions.SettingsCheckInterval)}", options.SettingsCheckInterval),
                options.GeoField is null ? null : FieldNameFailure($"{key}:{nameof(MeilisearchOptions.GeoField)}", options.GeoField),
            }.OfType<string>())
            .Concat((options.PhraseFields ?? []).Select((field, i) =>
                FieldNameFailure($"{key}:{nameof(MeilisearchOptions.PhraseFields)}:{i}", field)).OfType<string>())
            .Concat(BulkBatchSizeFailures($"{key}:{nameof(MeilisearchOptions.SharedIndexSettings)}", options.SharedIndexSettings))
            .Concat(RequestTimeoutFailures($"{key}:{nameof(MeilisearchOptions.SharedIndexSettings)}", options.SharedIndexSettings))
            .Concat((options.Indexes ?? []).SelectMany(index =>
                RequestTimeoutFailures($"{key}:{nameof(MeilisearchOptions.Indexes)}:{index.Key}", index.Value)))
            .Concat((options.Indexes ?? []).SelectMany(index =>
                BulkBatchSizeFailures($"{key}:{nameof(MeilisearchOptions.Indexes)}:{index.Key}", index.Value)))
            .ToList();
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>
    /// Options with no connection string or <c>Indexes</c> entry at all fail once, naming the section.
    /// Otherwise the shared settings are checked as an index not listed under <c>Indexes</c> gets them, unless they set no
    /// connection string and every index is listed with its own. An <c>Indexes</c> entry with its own connection
    /// string is checked after merging; one without has the shared settings, which are checked already.
    /// </summary>
    private static IEnumerable<string> ConnectionFailures(MeilisearchOptions options)
    {
        var shared = options.SharedIndexSettings ?? new MeilisearchIndexSettings();
        var indexes = options.Indexes ?? [];
        var sharedConnection = !string.IsNullOrWhiteSpace(shared.ConnectionString);
        if (string.IsNullOrEmpty(shared.ConnectionString) && indexes.Count == 0)
        {
            return [ConnectionSettingsChecks.NoSettingsFailure(MeilisearchOptions.MeilisearchOptionsKey)];
        }

        var withOwnConnection = indexes.Where(index => !string.IsNullOrWhiteSpace(index.Value?.ConnectionString)).ToList();
        var sharedFailures = sharedConnection || indexes.Count == 0 || withOwnConnection.Count < indexes.Count
            ? MeilisearchClientSettingsManager.ConnectionFailures(shared, null)
            : [];
        return sharedFailures.Concat(withOwnConnection.SelectMany(index => MeilisearchClientSettingsManager.ConnectionFailures(
            MeilisearchClientSettingsManager.MergeSettings(index.Value, shared), index.Key)));
    }

    private static string? FieldNameFailure(string path, string? field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return $"{path} is empty, but it must name a document field.";
        }

        return MeilisearchFieldNames.IsWritable(field)
            ? null
            : $"{path} {field} can only contain letters, digits, '_', '-' and '.', as Meilisearch filters write field names unquoted.";
    }

    private static IEnumerable<string> BulkBatchSizeFailures(string path, MeilisearchIndexSettings? settings) =>
        settings?.BulkBatchSize is <= 0
            ? [$"{path}:{nameof(MeilisearchIndexSettings.BulkBatchSize)} is {settings.BulkBatchSize}, but it must be greater than 0."]
            : [];

    private static IEnumerable<string> RequestTimeoutFailures(string path, MeilisearchIndexSettings? settings) =>
        settings?.RequestTimeout is { } timeout
            ? new[] { ConnectionSettingsChecks.RequestTimeoutFailure($"{path}:{nameof(MeilisearchIndexSettings.RequestTimeout)}", timeout) }.OfType<string>()
            : [];
}
