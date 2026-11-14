using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Meilisearch.DepedencyInjection;
using MeilisearchClient = Meilisearch.MeilisearchClient;
using Pagination = Meilisearch.Pagination;
using TaskInfoStatus = Meilisearch.TaskInfoStatus;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bielu.BetterSearch.Meilisearch.Tests;

/// <summary>Counting against a real Meilisearch, on an index whose <c>maxTotalHits</c> is lower than the count.</summary>
public sealed class MeilisearchCountTests(MeilisearchResource meilisearch)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The index's <c>maxTotalHits</c> is lowered to 10 after its 15 documents are indexed.</summary>
    [Fact]
    public async Task CountAsync_MoreMatchesThanMaxTotalHits_ReturnsExactCount()
    {
        var services = new ServiceCollection();
        services.AddLogging().AddBetterSearch(x => x.AddMeilisearch());
        services.AddOptions<MeilisearchOptions>().Configure(options =>
        {
            options.SharedIndexSettings = new MeilisearchIndexSettings
            {
                ConnectionString = meilisearch.Endpoint!.ToString(),
                ApiKey = meilisearch.MasterKey,
            };
            options.MaxTotalHits = 10;
        });
        await using var provider = services.BuildServiceProvider();
        var indexingProvider = provider.GetRequiredService<IIndexingProviderAsync>();
        var searchService = provider.GetRequiredService<ISearchServiceAsync>();
        var index = $"count-{Guid.NewGuid():N}";
        try
        {
            var indexed = await indexingProvider.IndexMultipleDocumentsAsync(Enumerable.Range(0, 15).Select(i => new SearchDocument
            {
                Id = $"d{i}",
                Type = "Item",
                Index = index,
                Fields = new Dictionary<string, IList<object>> { { "Rank", [(long)i] } }
            }), Token);
            indexed.IsSuccess.ShouldBeTrue(string.Join("; ", indexed.Errors.Select(error => error.Message)));
            var client = await provider.GetRequiredService<IClientFactoryAsync<MeilisearchClient>>().GetOrCreateClientAsync(index);
            var lowered = await client.Index(index).UpdatePaginationAsync(new Pagination { MaxTotalHits = 10 }, Token);
            (await client.WaitForTaskAsync(lowered.TaskUid, cancellationToken: Token)).Status.ShouldBe(TaskInfoStatus.Succeeded);

            var count = await searchService.CountAsync(new SearchQuery { Index = index }, Token);

            count.IsSuccess.ShouldBeTrue(string.Join("; ", count.Errors.Select(error => error.Message)));
            count.Value.ShouldBe(15);
        }
        finally
        {
            try
            {
                await indexingProvider.DeleteIndexAsync(index, CancellationToken.None);
            }
            catch
            {
                // Cleanup must not hide the test's own failure.
            }
        }
    }
}
