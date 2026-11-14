using System.Text.Json;
using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.DepedencyInjection;
using Bielu.BetterSearch.Tests;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

/// <summary>The index prefix and rebuilds against a real Elasticsearch, with a prefix and a configured mapping.</summary>
public sealed class ElasticsearchIndexLifecycleTests(ElasticsearchResource elasticsearch) : IndexLifecycleTestBase(CreateServices(elasticsearch))
{
    private const string IndexPrefix = "lifecycle-test-";

    protected override string Prefix => IndexPrefix;

    private static ServiceProvider CreateServices(ElasticsearchResource elasticsearch)
    {
        var services = new ServiceCollection();
        services.AddLogging().AddBetterSearch(x => x.AddElasticSearch());
        services.AddOptions<ElasticSearchOptions>().Configure(options =>
        {
            options.IndexPrefix = IndexPrefix;
            options.SharedIndexSettings = new ElasticSearchIndexSettings
            {
                ConnectionString = elasticsearch.EnsureStarted().ToString(),
                AuthenticationType = AuthenticationType.NONE,
                WriteVisibility = WriteVisibility.WaitUntilSearchable,
                Mappings = [new ElasticSearchFieldMapping { Field = "City", Type = ElasticSearchFieldType.Keyword }],
            };
        });
        return services.BuildServiceProvider();
    }

    private async Task<StringResponse> GetAsync(string path)
    {
        var client = await Services.GetRequiredService<IClientFactoryAsync<ElasticsearchClient>>().GetOrCreateClientAsync("any");
        return await client.Transport.RequestAsync<StringResponse>(Elastic.Transport.HttpMethod.GET, path, cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>The top-level property names of a 200 response, which name indexes for these requests; empty for a 404.</summary>
    private async Task<IReadOnlyList<string>> IndexNamesAsync(string path)
    {
        var response = await GetAsync(path);
        if (response.ApiCallDetails.HttpStatusCode == 404)
        {
            return [];
        }

        response.ApiCallDetails.HttpStatusCode.ShouldBe(200, response.Body);
        return JsonDocument.Parse(response.Body).RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToList();
    }

    // Getting an alias returns the indexes behind it, so only an index of exactly this name counts.
    protected override async Task<bool> EngineIndexExistsAsync(string engineName) =>
        (await IndexNamesAsync($"/{engineName}")).Contains(engineName);

    protected override Task<IReadOnlyList<string>> AliasTargetsAsync(string alias) => IndexNamesAsync($"/_alias/{alias}");

    [Fact]
    public async Task StartRebuild_ConfiguredMappings_RebuildIndexHasThem()
    {
        var lifecycle = Services.GetRequiredService<IIndexLifecycleServiceAsync>();
        var index = $"mapped-{Guid.NewGuid():N}";
        var rebuild = await lifecycle.StartRebuildAsync(index, TestContext.Current.CancellationToken);
        try
        {
            rebuild.IsSuccess.ShouldBeTrue(string.Join("; ", rebuild.Errors.Select(error => error.Message)));

            var response = await GetAsync($"/{rebuild.Value.TargetIndex}/_mapping");

            response.ApiCallDetails.HttpStatusCode.ShouldBe(200, response.Body);
            JsonDocument.Parse(response.Body).RootElement
                .GetProperty(rebuild.Value.TargetIndex).GetProperty("mappings").GetProperty("properties")
                .GetProperty("fields").GetProperty("properties").GetProperty("City").GetProperty("type").GetString()
                .ShouldBe("keyword");
        }
        finally
        {
            try
            {
                if (rebuild.IsSuccess)
                {
                    await lifecycle.AbortRebuildAsync(rebuild.Value, CancellationToken.None);
                }
            }
            catch (Exception)
            {
                // Cleanup must not hide the test's own failure.
            }
        }
    }

    protected override void Dispose(bool disposing) => (Services as IDisposable)?.Dispose();
}
