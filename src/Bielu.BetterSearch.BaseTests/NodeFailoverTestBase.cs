using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

/// <summary>
/// Requests against a real engine through a pool whose first node can't be reached, inherited by the tests of each
/// provider that takes several nodes.
/// </summary>
public abstract class NodeFailoverTestBase
{
    /// <summary>A node nothing listens on: the connection to port 1 is refused.</summary>
    protected const string UnreachableNode = "http://127.0.0.1:1";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The URL of the running engine.</summary>
    protected abstract string EngineNode { get; }

    /// <summary>
    /// Services connecting to <paramref name="connectionString"/> and then <paramref name="additionalNodes"/>, in that
    /// order every time, with writes that return once they are searchable.
    /// </summary>
    protected abstract ServiceProvider CreateServices(string connectionString, IList<string> additionalNodes);

    private static SearchDocument Document(string index, string id) => new()
    {
        Id = id,
        Type = "TestDocument",
        Index = index,
        Fields = new Dictionary<string, IList<object>> { { "Name", [$"item {id}"] } }
    };

    private static void ShouldSucceed(ResultBase result) =>
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));

    [Fact]
    public async Task Writes_FirstNodeUnreachable_UseTheNextNode()
    {
        using var services = CreateServices(UnreachableNode, [EngineNode]);
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IIndexingProviderAsync>();
        var indexing = scope.ServiceProvider.GetRequiredService<IIndexingServiceAsync>();
        var search = scope.ServiceProvider.GetRequiredService<ISearchServiceAsync>();
        var index = $"failover-{Guid.NewGuid():N}";
        try
        {
            var ensured = await provider.EnsureIndexExistsAsync(index, Token);
            ShouldSucceed(ensured);
            ensured.Value.ShouldBeTrue();

            ShouldSucceed(await indexing.IndexDocumentAsync(Document(index, "a"), Token));

            var ids = await search.SearchIdsAsync(SearchTestHelpers.Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "item" }), Token);
            ids.ShouldBe(["a"]);
        }
        finally
        {
            try
            {
                await provider.DeleteIndexAsync(index, CancellationToken.None);
            }
            catch (Exception)
            {
                // Cleanup must not hide the test's own failure.
            }
        }
    }

    /// <summary>Shows that <see cref="UnreachableNode"/> really can't be reached, so the test above has to fail over.</summary>
    [Fact]
    public async Task EnsureIndexExistsAsync_OnlyNodeUnreachable_ReturnsFailedResult()
    {
        using var services = CreateServices(UnreachableNode, []);
        var provider = services.GetRequiredService<IIndexingProviderAsync>();

        var ensured = await provider.EnsureIndexExistsAsync($"failover-{Guid.NewGuid():N}", Token);

        ensured.IsFailed.ShouldBeTrue();
    }
}
