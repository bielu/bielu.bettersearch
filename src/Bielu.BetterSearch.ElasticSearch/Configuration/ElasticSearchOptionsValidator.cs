using Bielu.BetterSearch.Services;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.ElasticSearch.Configuration;

/// <summary>Checks the field mappings, bulk batch sizes and the index prefix, at startup and whenever the options are read, naming each setting that is wrong.</summary>
internal sealed class ElasticSearchOptionsValidator : IValidateOptions<ElasticSearchOptions>
{
    public ValidateOptionsResult Validate(string? name, ElasticSearchOptions options)
    {
        var key = ElasticSearchOptions.ElasticSearchOptionsKey;
        var failures = FieldMappings.Validate($"{key}:{nameof(ElasticSearchOptions.SharedIndexSettings)}", options.SharedIndexSettings?.Mappings?.ToList<ElasticSearchFieldMapping?>())
            .Concat((options.Indexes ?? []).SelectMany(index =>
                FieldMappings.Validate($"{key}:{nameof(ElasticSearchOptions.Indexes)}:{index.Key}", index.Value?.Mappings?.ToList<ElasticSearchFieldMapping?>())))
            .Concat(BulkBatchSizeFailures($"{key}:{nameof(ElasticSearchOptions.SharedIndexSettings)}", options.SharedIndexSettings))
            .Concat((options.Indexes ?? []).SelectMany(index =>
                BulkBatchSizeFailures($"{key}:{nameof(ElasticSearchOptions.Indexes)}:{index.Key}", index.Value)))
            .Concat(IndexNames.ValidatePrefix($"{key}:{nameof(ElasticSearchOptions.IndexPrefix)}", options.IndexPrefix))
            .ToList();
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static IEnumerable<string> BulkBatchSizeFailures(string path, ElasticSearchIndexSettings? settings) =>
        settings?.BulkBatchSize is <= 0
            ? [$"{path}:{nameof(ElasticSearchIndexSettings.BulkBatchSize)} is {settings.BulkBatchSize}, but it must be greater than 0."]
            : [];
}
