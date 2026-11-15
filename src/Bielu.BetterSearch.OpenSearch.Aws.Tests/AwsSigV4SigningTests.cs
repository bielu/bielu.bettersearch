using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Amazon.Runtime;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.DependencyInjection;
using Bielu.BetterSearch.OpenSearch.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenSearch.Client;
using OpenSearch.Net;
using Shouldly;
using HttpMethod = OpenSearch.Net.HttpMethod;

namespace Bielu.BetterSearch.OpenSearch.Aws.Tests;

/// <summary>
/// Sends requests through the OpenSearch provider with <c>AddOpenSearchAwsSigV4</c> to a local endpoint, with known
/// credentials, region, service and clock, and checks the signed request. Not run against Amazon OpenSearch Service.
/// </summary>
public partial class AwsSigV4SigningTests
{
    private const string AccessKey = "AKIDEXAMPLE";
    private const string SecretKey = "wJalrXUtnFEMI/K7MDENG+bPxRfiCYEXAMPLEKEY";
    private const string SessionToken = "session-token-example";
    private static readonly DateTime Now = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    [GeneratedRegex(@"^AWS4-HMAC-SHA256 Credential=(?<credential>[^,]+), SignedHeaders=(?<signedHeaders>[^,]+), Signature=(?<signature>[0-9a-f]{64})$")]
    private static partial Regex AuthorizationPattern();

    private static async Task<ReceivedRequest> SendAsync(FakeOpenSearchEndpoint endpoint, string service, AWSCredentials credentials,
        string body, IDictionary<string, string?>? configuration = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Bielu:BetterSearch:OpenSearch:SharedIndexSettings:ConnectionString"] = endpoint.Url,
            ["Bielu:BetterSearch:OpenSearch:AwsSigV4:Region"] = "eu-west-1",
            ["Bielu:BetterSearch:OpenSearch:AwsSigV4:Service"] = service,
        };
        foreach (var setting in configuration ?? new Dictionary<string, string?>())
        {
            settings[setting.Key] = setting.Value;
        }

        await using var services = new ServiceCollection()
            .AddBetterSearch(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), search => search
                .AddOpenSearch()
                .AddOpenSearchAwsSigV4(options =>
                {
                    options.Credentials = credentials;
                    options.DateTimeProvider = new FixedClock(Now);
                }))
            .BuildServiceProvider();
        var client = await services.GetRequiredService<IClientFactoryAsync<IOpenSearchClient>>().GetOrCreateClientAsync("people");

        var response = await client.LowLevel.DoRequestAsync<StringResponse>(HttpMethod.POST, "people/_search",
            TestContext.Current.CancellationToken, PostData.String(body));

        response.Success.ShouldBeTrue(response.DebugInformation);
        return endpoint.Requests.ShouldHaveSingleItem();
    }

    private static (string Credential, string[] SignedHeaders, string Signature) Authorization(ReceivedRequest request)
    {
        var match = AuthorizationPattern().Match(request.Headers["Authorization"]);
        match.Success.ShouldBeTrue(request.Headers["Authorization"]);
        return (match.Groups["credential"].Value, match.Groups["signedHeaders"].Value.Split(';'), match.Groups["signature"].Value);
    }

    private static string ExpectedSignature(ReceivedRequest request, string service, string[] signedHeaders) =>
        ReferenceSigV4.Signature(SecretKey, "20260102", "eu-west-1", service, "20260102T030405Z",
            request.Method, request.Path, request.Query,
            signedHeaders.Select(name => new KeyValuePair<string, string>(name, request.Headers[name])).ToList(),
            request.Headers["X-Amz-Content-Sha256"]);

    [Theory]
    [InlineData(OpenSearchAwsSigV4Options.OpenSearchService)]
    [InlineData(OpenSearchAwsSigV4Options.OpenSearchServerlessService)]
    public async Task AddOpenSearchAwsSigV4_StaticCredentials_SignsTheRequestForTheRegionAndService(string service)
    {
        using var endpoint = new FakeOpenSearchEndpoint();
        const string body = """{"query":{"match_all":{}}}""";

        var request = await SendAsync(endpoint, service, new BasicAWSCredentials(AccessKey, SecretKey), body);

        var (credential, signedHeaders, signature) = Authorization(request);
        credential.ShouldBe($"{AccessKey}/20260102/eu-west-1/{service}/aws4_request");
        signedHeaders.ShouldContain("host");
        signedHeaders.ShouldContain("x-amz-date");
        signedHeaders.ShouldContain("x-amz-content-sha256");
        signedHeaders.ShouldNotContain("x-amz-security-token");
        request.Headers["X-Amz-Date"].ShouldBe("20260102T030405Z");
        request.Headers["X-Amz-Content-Sha256"].ShouldBe(ReferenceSigV4.Hex(SHA256.HashData(request.Body)));
        System.Text.Encoding.UTF8.GetString(request.Body).ShouldBe(body);
        request.Headers.ContainsKey("X-Amz-Security-Token").ShouldBeFalse();
        signature.ShouldBe(ExpectedSignature(request, service, signedHeaders));
    }

    [Fact]
    public async Task AddOpenSearchAwsSigV4_SessionCredentials_SignsTheSecurityToken()
    {
        using var endpoint = new FakeOpenSearchEndpoint();

        var request = await SendAsync(endpoint, "es", new SessionAWSCredentials(AccessKey, SecretKey, SessionToken), "{}");

        var (credential, signedHeaders, signature) = Authorization(request);
        credential.ShouldBe($"{AccessKey}/20260102/eu-west-1/es/aws4_request");
        request.Headers["X-Amz-Security-Token"].ShouldBe(SessionToken);
        signedHeaders.ShouldContain("x-amz-security-token");
        signature.ShouldBe(ExpectedSignature(request, "es", signedHeaders));
    }

    [Fact]
    public async Task AddOpenSearchAwsSigV4_DifferentBody_ChangesTheContentHashAndTheSignature()
    {
        using var first = new FakeOpenSearchEndpoint();
        using var second = new FakeOpenSearchEndpoint();
        var credentials = new BasicAWSCredentials(AccessKey, SecretKey);

        var one = await SendAsync(first, "es", credentials, """{"query":{"term":{"fields.Name":"jane"}}}""");
        var other = await SendAsync(second, "es", credentials, """{"query":{"term":{"fields.Name":"john"}}}""");

        other.Headers["X-Amz-Content-Sha256"].ShouldNotBe(one.Headers["X-Amz-Content-Sha256"]);
        other.Headers["X-Amz-Content-Sha256"].ShouldBe(ReferenceSigV4.Hex(SHA256.HashData(other.Body)));
        var (_, signedHeaders, signature) = Authorization(other);
        signature.ShouldNotBe(Authorization(one).Signature);
        signature.ShouldBe(ExpectedSignature(other, "es", signedHeaders));
    }

    [Fact]
    public async Task AddOpenSearchAwsSigV4_RegionInConfigureOverridesConfiguration_SignsForTheConfiguredRegion()
    {
        using var endpoint = new FakeOpenSearchEndpoint();
        var settings = new Dictionary<string, string?>
        {
            ["Bielu:BetterSearch:OpenSearch:SharedIndexSettings:ConnectionString"] = endpoint.Url,
            ["Bielu:BetterSearch:OpenSearch:AwsSigV4:Region"] = "eu-west-1",
        };
        await using var services = new ServiceCollection()
            .AddBetterSearch(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), search => search
                .AddOpenSearchAwsSigV4(options =>
                {
                    options.Region = "us-east-2";
                    options.Credentials = new BasicAWSCredentials(AccessKey, SecretKey);
                    options.DateTimeProvider = new FixedClock(Now);
                })
                .AddOpenSearch())
            .BuildServiceProvider();
        var client = await services.GetRequiredService<IClientFactoryAsync<IOpenSearchClient>>().GetOrCreateClientAsync("people");

        var response = await client.LowLevel.DoRequestAsync<StringResponse>(HttpMethod.GET, "people",
            TestContext.Current.CancellationToken);

        response.Success.ShouldBeTrue(response.DebugInformation);
        var (credential, _, _) = Authorization(endpoint.Requests.ShouldHaveSingleItem());
        credential.ShouldBe($"{AccessKey}/20260102/us-east-2/es/aws4_request");
    }
}
