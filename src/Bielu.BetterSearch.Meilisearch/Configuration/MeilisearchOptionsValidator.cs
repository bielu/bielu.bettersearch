using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.Meilisearch.Configuration;

/// <summary>Checks the bulk batch sizes, at startup and whenever the options are read, naming each setting that is wrong.</summary>
internal sealed class MeilisearchOptionsValidator : IValidateOptions<MeilisearchOptions>
{
    public ValidateOptionsResult Validate(string? name, MeilisearchOptions options)
    {
        var key = MeilisearchOptions.MeilisearchOptionsKey;
        var failures = BulkBatchSizeFailures($"{key}:{nameof(MeilisearchOptions.SharedIndexSettings)}", options.SharedIndexSettings)
            .Concat((options.Indexes ?? []).SelectMany(index =>
                BulkBatchSizeFailures($"{key}:{nameof(MeilisearchOptions.Indexes)}:{index.Key}", index.Value)))
            .ToList();
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static IEnumerable<string> BulkBatchSizeFailures(string path, MeilisearchIndexSettings? settings) =>
        settings?.BulkBatchSize is <= 0
            ? [$"{path}:{nameof(MeilisearchIndexSettings.BulkBatchSize)} is {settings.BulkBatchSize}, but it must be greater than 0."]
            : [];
}
