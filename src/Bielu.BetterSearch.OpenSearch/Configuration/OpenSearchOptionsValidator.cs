using Bielu.BetterSearch.Services;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.OpenSearch.Configuration;

/// <summary>Checks the field mappings, at startup and whenever the options are read, naming each setting that is wrong.</summary>
internal sealed class OpenSearchOptionsValidator : IValidateOptions<OpenSearchOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenSearchOptions options)
    {
        var key = OpenSearchOptions.OpenSearchOptionsKey;
        var failures = FieldMappings.Validate($"{key}:{nameof(OpenSearchOptions.SharedIndexSettings)}", options.SharedIndexSettings?.Mappings?.ToList<OpenSearchFieldMapping?>())
            .Concat((options.Indexes ?? []).SelectMany(index =>
                FieldMappings.Validate($"{key}:{nameof(OpenSearchOptions.Indexes)}:{index.Key}", index.Value?.Mappings?.ToList<OpenSearchFieldMapping?>())))
            .ToList();
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
