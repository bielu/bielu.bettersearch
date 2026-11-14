using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

/// <summary>
/// Writes through <see cref="IIndexingServiceAsync"/> with each provider configured so that a successful write
/// returns only once it is searchable, inherited by every provider's tests. Each write is searched for straight
/// away, with no sleep and no manual refresh.
/// </summary>
/// <param name="services">
/// Services whose provider waits until writes are searchable: configured with
/// <c>WriteVisibility.WaitUntilSearchable</c> where the engine needs it, or the default services where it already waits.
/// </param>
public abstract class SearchableWritesTestBase(IServiceProvider services) : IDisposable
{
    private readonly IServiceScope _scope = services.CreateScope();

    /// <summary>The services the tests run on.</summary>
    protected IServiceProvider Services { get; } = services;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private IIndexingServiceAsync Indexing => _scope.ServiceProvider.GetRequiredService<IIndexingServiceAsync>();
    private IIndexingProviderAsync Provider => _scope.ServiceProvider.GetRequiredService<IIndexingProviderAsync>();
    private ISearchServiceAsync Search => _scope.ServiceProvider.GetRequiredService<ISearchServiceAsync>();

    private static SearchDocument Document(string index, string id) => new()
    {
        Id = id,
        Type = "TestDocument",
        Index = index,
        Fields = new Dictionary<string, IList<object>> { { "Name", [$"item {id}"] } }
    };

    /// <summary>The ids of every document in <paramref name="index"/>, each named <c>item</c> and its id.</summary>
    private Task<List<string>> IdsAsync(string index) =>
        Search.SearchIdsAsync(SearchTestHelpers.Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "item" }), Token);

    private static void ShouldSucceed(ResultBase result) =>
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));

    [Fact]
    public async Task Writes_WaitUntilSearchable_AreSearchableWhenTheCallReturns()
    {
        var index = $"searchable-{Guid.NewGuid():N}";
        try
        {
            var ensured = await Provider.EnsureIndexExistsAsync(index, Token);
            ShouldSucceed(ensured);
            ensured.Value.ShouldBeTrue();
            (await IdsAsync(index)).ShouldBeEmpty();

            // One write at a time, so each has to be searchable before the next starts.
            foreach (var id in new[] { "a", "b", "c" })
            {
                ShouldSucceed(await Indexing.IndexDocumentAsync(Document(index, id), Token));
                (await IdsAsync(index)).ShouldContain(id);
            }

            var indexed = await Indexing.IndexMultipleDocumentsAsync([Document(index, "d"), Document(index, "e")], Token);
            ShouldSucceed(indexed);
            indexed.Value.ShouldBe(2);
            (await IdsAsync(index)).ShouldBe(["a", "b", "c", "d", "e"]);

            ShouldSucceed(await Indexing.RemoveDocumentAsync(new DeleteDocumentRequest { Id = "a", Type = index }, Token));
            (await IdsAsync(index)).ShouldBe(["b", "c", "d", "e"]);

            var removed = await Indexing.RemoveDocumentsAsync(index, ["b", "c"], Token);
            ShouldSucceed(removed);
            removed.Value.ShouldBe(2);
            (await IdsAsync(index)).ShouldBe(["d", "e"]);

            var removedAll = await Indexing.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = index }, Token);
            ShouldSucceed(removedAll);
            (await IdsAsync(index)).ShouldBeEmpty();
        }
        finally
        {
            try
            {
                await Provider.DeleteIndexAsync(index, CancellationToken.None);
            }
            catch (Exception)
            {
                // Cleanup must not hide the test's own failure.
            }
        }
    }

    public void Dispose()
    {
        _scope.Dispose();
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Disposes what the derived class created, such as a service provider of its own.</summary>
    protected virtual void Dispose(bool disposing)
    {
    }
}
