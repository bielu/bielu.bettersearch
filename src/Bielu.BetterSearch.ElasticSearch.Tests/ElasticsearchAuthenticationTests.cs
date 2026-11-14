using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.DepedencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

/// <summary>
/// Each authentication scheme and TLS setting against an Elasticsearch with security on and the self-signed
/// certificate it generates, through <see cref="IIndexingServiceAsync"/> and <see cref="ISearchServiceAsync"/>.
/// </summary>
public class ElasticsearchAuthenticationTests(SecuredElasticsearchResource elasticsearch)
{
    private const string Key = "Bielu:BetterSearch:ElasticSearch:SharedIndexSettings";

    [Fact]
    public async Task IndexAndGetDocument_ApiKeyWithCertificateFingerprint_RoundTrips()
    {
        await elasticsearch.EnsureStartedAsync();

        await AssertRoundTripAsync(new Dictionary<string, string?>
        {
            [$"{Key}:AuthenticationType"] = "APIKEY",
            [$"{Key}:AuthenticationDetails:ApiKey"] = elasticsearch.ApiKey,
            [$"{Key}:CertificateFingerprint"] = elasticsearch.CaFingerprint,
        });
    }

    [Fact]
    public async Task IndexAndGetDocument_BearerServiceAccountTokenWithColonSeparatedLowerCaseFingerprint_RoundTrips()
    {
        await elasticsearch.EnsureStartedAsync();

        await AssertRoundTripAsync(new Dictionary<string, string?>
        {
            [$"{Key}:AuthenticationType"] = "BEARER",
            [$"{Key}:AuthenticationDetails:Token"] = elasticsearch.ServiceAccountToken,
            [$"{Key}:CertificateFingerprint"] = string.Join(":", elasticsearch.CaFingerprint.ToLowerInvariant().Chunk(2).Select(pair => new string(pair))),
        });
    }

    [Fact]
    public async Task IndexAndGetDocument_PasswordWithSkippedCertificateValidation_RoundTrips()
    {
        await elasticsearch.EnsureStartedAsync();

        await AssertRoundTripAsync(new Dictionary<string, string?>
        {
            [$"{Key}:AuthenticationType"] = "PASSWORD",
            [$"{Key}:AuthenticationDetails:Username"] = "elastic",
            [$"{Key}:AuthenticationDetails:Password"] = elasticsearch.Password,
            [$"{Key}:DevelopmentOnlySkipCertificateValidation"] = "true",
        });
    }

    [Fact]
    public async Task IndexDocument_ApiKeyWithoutTlsSettings_FailsOnTheUntrustedCertificate()
    {
        await elasticsearch.EnsureStartedAsync();

        var result = await IndexAsync(new Dictionary<string, string?>
        {
            [$"{Key}:AuthenticationType"] = "APIKEY",
            [$"{Key}:AuthenticationDetails:ApiKey"] = elasticsearch.ApiKey,
        }, $"content-bettersearch-{Guid.NewGuid():N}");

        result.IsFailed.ShouldBeTrue();
        AllMessages(result).ShouldContain("SSL", Case.Insensitive);
    }

    [Fact]
    public async Task IndexDocument_ApiKeyWithAnotherCertificateFingerprint_FailsOnTheCertificate()
    {
        await elasticsearch.EnsureStartedAsync();

        var result = await IndexAsync(new Dictionary<string, string?>
        {
            [$"{Key}:AuthenticationType"] = "APIKEY",
            [$"{Key}:AuthenticationDetails:ApiKey"] = elasticsearch.ApiKey,
            [$"{Key}:CertificateFingerprint"] = new string('A', 64),
        }, $"content-bettersearch-{Guid.NewGuid():N}");

        result.IsFailed.ShouldBeTrue();
        AllMessages(result).ShouldContain("SSL", Case.Insensitive);
    }

    [Theory]
    [InlineData("APIKEY", "ApiKey")]
    [InlineData("BEARER", "Token")]
    public async Task IndexDocument_WrongCredential_FailsWithUnauthorized(string authenticationType, string detail)
    {
        await elasticsearch.EnsureStartedAsync();

        var result = await IndexAsync(new Dictionary<string, string?>
        {
            [$"{Key}:AuthenticationType"] = authenticationType,
            [$"{Key}:AuthenticationDetails:{detail}"] = "bm90LWEta2V5",
            [$"{Key}:CertificateFingerprint"] = elasticsearch.CaFingerprint,
        }, $"content-bettersearch-{Guid.NewGuid():N}");

        result.IsFailed.ShouldBeTrue();
        AllMessages(result).ShouldContain("401");
    }

    private async Task AssertRoundTripAsync(Dictionary<string, string?> settings)
    {
        // The service account may only use indexes named content-*.
        var index = $"content-bettersearch-{Guid.NewGuid():N}";
        try
        {
            using var provider = CreateProvider(settings);
            var indexed = await provider.GetRequiredService<IIndexingServiceAsync>()
                .IndexDocumentAsync(Document(index), TestContext.Current.CancellationToken);
            indexed.IsSuccess.ShouldBeTrue(AllMessages(indexed));

            var document = await provider.GetRequiredService<ISearchServiceAsync>()
                .GetDocumentAsync(index, "1", TestContext.Current.CancellationToken);

            document.IsSuccess.ShouldBeTrue(AllMessages(document.ToResult()));
            document.Value.ShouldNotBeNull().Fields["Name"].Single().ShouldBe("Ada");
        }
        finally
        {
            try
            {
                await elasticsearch.DeleteIndexAsync(index);
            }
            catch (Exception)
            {
                // Cleanup must not hide the test's own failure.
            }
        }
    }

    private async Task<FluentResults.Result> IndexAsync(Dictionary<string, string?> settings, string index)
    {
        using var provider = CreateProvider(settings);
        return await provider.GetRequiredService<IIndexingServiceAsync>()
            .IndexDocumentAsync(Document(index), TestContext.Current.CancellationToken);
    }

    private ServiceProvider CreateProvider(Dictionary<string, string?> settings)
    {
        settings[$"{Key}:ConnectionString"] = elasticsearch.Endpoint.ToString();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddLogging().AddBetterSearch(configuration, search => search.AddElasticSearch()).BuildServiceProvider();
    }

    private static SearchDocument Document(string index) => new()
    {
        Id = "1",
        Index = index,
        Fields = new Dictionary<string, IList<object>> { ["Name"] = ["Ada"] },
    };

    private static string AllMessages(FluentResults.ResultBase result) =>
        string.Join(" | ", result.Errors.SelectMany(Flatten));

    private static IEnumerable<string> Flatten(FluentResults.IError error) =>
        new[] { error.Message }
            .Concat(error is FluentResults.ExceptionalError { Exception: { } e } ? [e.ToString()] : [])
            .Concat(error.Reasons.OfType<FluentResults.IError>().SelectMany(Flatten));
}
