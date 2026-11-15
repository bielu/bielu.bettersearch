using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.OpenSearch.Aws;

/// <summary>Checks the service and region at startup and whenever the options are read, naming each setting that is wrong.</summary>
internal sealed class OpenSearchAwsSigV4OptionsValidator : IValidateOptions<OpenSearchAwsSigV4Options>
{
    public ValidateOptionsResult Validate(string? name, OpenSearchAwsSigV4Options options)
    {
        var failures = Failures(options).ToList();
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    internal static IEnumerable<string> Failures(OpenSearchAwsSigV4Options options)
    {
        var key = OpenSearchAwsSigV4Options.AwsSigV4OptionsKey;
        if (options.Service is not (OpenSearchAwsSigV4Options.OpenSearchService or OpenSearchAwsSigV4Options.OpenSearchServerlessService))
        {
            yield return $"Service is '{options.Service}', but must be '{OpenSearchAwsSigV4Options.OpenSearchService}' for Amazon OpenSearch Service " +
                         $"or '{OpenSearchAwsSigV4Options.OpenSearchServerlessService}' for Amazon OpenSearch Serverless. Set {key}:{nameof(OpenSearchAwsSigV4Options.Service)}.";
        }

        if (options.Region is not null && string.IsNullOrWhiteSpace(options.Region))
        {
            yield return $"Region is empty. Set {key}:{nameof(OpenSearchAwsSigV4Options.Region)} to a region such as eu-west-1, or remove it to let the AWS SDK find the region.";
        }
    }
}
