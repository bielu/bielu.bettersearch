using Amazon.Runtime;
using OpenSearch.Net;

namespace Bielu.BetterSearch.OpenSearch.Aws;

/// <summary>
/// How <c>AddOpenSearchAwsSigV4</c> signs OpenSearch requests, bound from <c>Bielu:BetterSearch:OpenSearch:AwsSigV4</c>.
/// </summary>
public class OpenSearchAwsSigV4Options
{
    public const string AwsSigV4OptionsKey = "Bielu:BetterSearch:OpenSearch:AwsSigV4";

    /// <summary>The service code for Amazon OpenSearch Service.</summary>
    public const string OpenSearchService = "es";

    /// <summary>The service code for Amazon OpenSearch Serverless.</summary>
    public const string OpenSearchServerlessService = "aoss";

    /// <summary>
    /// The service the requests are signed for: <c>es</c> (the default) for an Amazon OpenSearch Service domain, or
    /// <c>aoss</c> for an Amazon OpenSearch Serverless collection.
    /// </summary>
    public string Service { get; set; } = OpenSearchService;

    /// <summary>
    /// The AWS region the requests are signed for, such as <c>eu-west-1</c>. Unset, the AWS SDK finds it, from
    /// <c>AWS_REGION</c> or the AWS profile.
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// The credentials the requests are signed with. Unset, the AWS SDK's default credential chain finds them, such as
    /// environment variables, the AWS profile, or the role of the container or instance. Set it in code; it is not
    /// read from configuration.
    /// </summary>
    public AWSCredentials? Credentials { get; set; }

    /// <summary>The clock the signatures are dated with; tests set a fixed one.</summary>
    internal IDateTimeProvider? DateTimeProvider { get; set; }
}
