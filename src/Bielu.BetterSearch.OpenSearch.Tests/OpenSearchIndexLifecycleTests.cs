using System.Text.Json;
using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.DepedencyInjection;
using Bielu.BetterSearch.Services;
using Bielu.BetterSearch.Tests;
using Microsoft.Extensions.DependencyInjection;
using OpenSearch.Client;
using OpenSearch.Net;
using Shouldly;

namespace Bielu.BetterSearch.OpenSearch.Tests;

/// <summary>The index prefix and rebuilds against a real OpenSearch, with a prefix and a configured mapping.</summary>
public sealed class OpenSearchIndexLifecycleTests(OpenSearchResource openSearch) : IndexLifecycleTestBase(CreateServices(openSearch))
{
    private const string IndexPrefix = "lifecycle-test-";

    protected override string Prefix => IndexPrefix;

    private static ServiceProvider CreateServices(OpenSearchResource openSearch)
    {
        var services = new ServiceCollection();
        services.AddLogging().AddBetterSearch(x => x.AddOpenSearch());
        services.AddOptions<OpenSearchOptions>().Configure(options =>
        {
            options.IndexPrefix = IndexPrefix;
            options.SharedIndexSettings = new OpenSearchIndexSettings
            {
                ConnectionString = openSearch.EnsureStarted().ToString(),
                AuthenticationType = OpenSearchAuthenticationType.NONE,
                WriteVisibility = WriteVisibility.WaitUntilSearchable,
                Mappings = [new OpenSearchFieldMapping { Field = "City", Type = OpenSearchFieldType.Keyword }],
            };
        });
        return services.BuildServiceProvider();
    }

    private async Task<StringResponse> GetAsync(string path)
    {
        var client = await Services.GetRequiredService<IClientFactoryAsync<IOpenSearchClient>>().GetOrCreateClientAsync("any");
        return await client.LowLevel.DoRequestAsync<StringResponse>(global::OpenSearch.Net.HttpMethod.GET, path, TestContext.Current.CancellationToken);
    }

    /// <summary>The top-level property names of a 200 response, which name indexes for these requests; empty for a 404.</summary>
    private async Task<IReadOnlyList<string>> IndexNamesAsync(string path)
    {
        var response = await GetAsync(path);
        if (response.HttpStatusCode == 404)
        {
            return [];
        }

        response.HttpStatusCode.ShouldBe(200, response.Body);
        return JsonDocument.Parse(response.Body).RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToList();
    }

    // Getting an alias returns the indexes behind it, so only an index of exactly this name counts.
    protected override async Task<bool> EngineIndexExistsAsync(string engineName) =>
        (await IndexNamesAsync($"/{engineName}")).Contains(engineName);

    protected override Task<IReadOnlyList<string>> AliasTargetsAsync(string alias) => IndexNamesAsync($"/_alias/{alias}");

    protected override async Task RemovePendingAliasAsync(IndexRebuild rebuild)
    {
        var client = await Services.GetRequiredService<IClientFactoryAsync<IOpenSearchClient>>().GetOrCreateClientAsync("any");
        var response = await client.LowLevel.DoRequestAsync<StringResponse>(global::OpenSearch.Net.HttpMethod.DELETE, $"/{rebuild.TargetIndex}/_alias/{IndexNames.RebuildPendingAlias(rebuild.TargetIndex)}", TestContext.Current.CancellationToken);
        response.HttpStatusCode.ShouldBe(200, response.Body);
    }

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

            response.HttpStatusCode.ShouldBe(200, response.Body);
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
