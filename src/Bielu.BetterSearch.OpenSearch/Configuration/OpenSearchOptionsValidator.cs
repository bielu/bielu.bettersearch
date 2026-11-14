using Bielu.BetterSearch.Services;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.OpenSearch.Configuration;

/// <summary>Checks the field mappings and bulk batch sizes, at startup and whenever the options are read, naming each setting that is wrong.</summary>
internal sealed class OpenSearchOptionsValidator : IValidateOptions<OpenSearchOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenSearchOptions options)
    {
        var key = OpenSearchOptions.OpenSearchOptionsKey;
        var failures = FieldMappings.Validate($"{key}:{nameof(OpenSearchOptions.SharedIndexSettings)}", options.SharedIndexSettings?.Mappings?.ToList<OpenSearchFieldMapping?>())
            .Concat((options.Indexes ?? []).SelectMany(index =>
                FieldMappings.Validate($"{key}:{nameof(OpenSearchOptions.Indexes)}:{index.Key}", index.Value?.Mappings?.ToList<OpenSearchFieldMapping?>())))
            .Concat(BulkBatchSizeFailures($"{key}:{nameof(OpenSearchOptions.SharedIndexSettings)}", options.SharedIndexSettings))
            .Concat((options.Indexes ?? []).SelectMany(index =>
                BulkBatchSizeFailures($"{key}:{nameof(OpenSearchOptions.Indexes)}:{index.Key}", index.Value)))
            .ToList();
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static IEnumerable<string> BulkBatchSizeFailures(string path, OpenSearchIndexSettings? settings) =>
        settings?.BulkBatchSize is <= 0
            ? [$"{path}:{nameof(OpenSearchIndexSettings.BulkBatchSize)} is {settings.BulkBatchSize}, but it must be greater than 0."]
            : [];
}
