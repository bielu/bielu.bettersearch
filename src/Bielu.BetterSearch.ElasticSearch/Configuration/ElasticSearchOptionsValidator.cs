using Bielu.BetterSearch.Services;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.ElasticSearch.Configuration;

/// <summary>Checks the field mappings, at startup and whenever the options are read, naming each setting that is wrong.</summary>
internal sealed class ElasticSearchOptionsValidator : IValidateOptions<ElasticSearchOptions>
{
    public ValidateOptionsResult Validate(string? name, ElasticSearchOptions options)
    {
        var key = ElasticSearchOptions.ElasticSearchOptionsKey;
        var failures = FieldMappings.Validate($"{key}:{nameof(ElasticSearchOptions.SharedIndexSettings)}", options.SharedIndexSettings?.Mappings?.ToList<ElasticSearchFieldMapping?>())
            .Concat((options.Indexes ?? []).SelectMany(index =>
                FieldMappings.Validate($"{key}:{nameof(ElasticSearchOptions.Indexes)}:{index.Key}", index.Value?.Mappings?.ToList<ElasticSearchFieldMapping?>())))
            .ToList();
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
