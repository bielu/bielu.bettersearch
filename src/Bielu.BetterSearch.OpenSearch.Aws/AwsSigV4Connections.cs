using Amazon;
using OpenSearch.Net;
using OpenSearch.Net.Auth.AwsSigV4;

namespace Bielu.BetterSearch.OpenSearch.Aws;

internal static class AwsSigV4Connections
{
    /// <summary>
    /// A connection that signs each request with AWS SigV4 for <paramref name="options"/>, throwing
    /// <see cref="ArgumentException"/>, which the provider returns as a failed <c>Result</c>, when the options are invalid
    /// or the AWS SDK can't find a region.
    /// </summary>
    public static IConnection Create(OpenSearchAwsSigV4Options options)
    {
        var failures = OpenSearchAwsSigV4OptionsValidator.Failures(options).ToList();
        if (failures.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", failures), nameof(options));
        }

        var region = options.Region is null ? null : RegionEndpoint.GetBySystemName(options.Region);
        try
        {
            return new AwsSigV4HttpConnection(options.Credentials, region, options.Service, options.DateTimeProvider);
        }
        catch (ArgumentNullException e) when (region is null)
        {
            throw new ArgumentException(
                $"The AWS SDK found no region to sign OpenSearch requests for. Set {OpenSearchAwsSigV4Options.AwsSigV4OptionsKey}:{nameof(OpenSearchAwsSigV4Options.Region)}, or AWS_REGION.",
                nameof(options), e);
        }
    }
}
