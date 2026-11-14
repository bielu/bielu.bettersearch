using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

/// <summary>
/// The index prefix and zero-downtime rebuilds through <see cref="IIndexLifecycleServiceAsync"/>, inherited by the
/// tests of every provider that implements <see cref="IIndexLifecycleProviderAsync"/>. Each test checks what the engine
/// holds by name, so a step that drops the prefix or deletes the wrong index fails.
/// </summary>
/// <param name="services">
/// Services whose provider has <see cref="Prefix"/> as its index prefix and waits until writes are searchable.
/// </param>
public abstract class IndexLifecycleTestBase(IServiceProvider services) : IDisposable
{
    private readonly IServiceScope _scope = services.CreateScope();

    /// <summary>The services the tests run on.</summary>
    protected IServiceProvider Services { get; } = services;

    /// <summary>The index prefix the services are configured with.</summary>
    protected abstract string Prefix { get; }

    /// <summary>Whether the engine has an index, not an alias, named exactly <paramref name="engineName"/>.</summary>
    protected abstract Task<bool> EngineIndexExistsAsync(string engineName);

    /// <summary>The indexes the alias <paramref name="alias"/> points at on the engine, in ordinal order; empty when there is no such alias.</summary>
    protected abstract Task<IReadOnlyList<string>> AliasTargetsAsync(string alias);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private IIndexingServiceAsync Indexing => _scope.ServiceProvider.GetRequiredService<IIndexingServiceAsync>();
    private IIndexingProviderAsync Provider => _scope.ServiceProvider.GetRequiredService<IIndexingProviderAsync>();
    private ISearchServiceAsync Search => _scope.ServiceProvider.GetRequiredService<ISearchServiceAsync>();
    private IIndexLifecycleServiceAsync Lifecycle => _scope.ServiceProvider.GetRequiredService<IIndexLifecycleServiceAsync>();

    private static SearchDocument Document(string index, string id) => new()
    {
        Id = id,
        Type = "TestDocument",
        Index = index,
        Fields = new Dictionary<string, IList<object>> { { "Name", [$"item {id}"] } }
    };

    private Task<List<string>> IdsAsync(string index) =>
        Search.SearchIdsAsync(SearchTestHelpers.Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "item" }), Token);

    private static void ShouldSucceed(ResultBase result) =>
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));

    private static string Errors(ResultBase result) => string.Join("; ", result.Errors.Select(error => error.Message));

    /// <summary>An index with documents <paramref name="ids"/>, created as a plain index, not an alias.</summary>
    private async Task<string> IndexWithDocumentsAsync(params string[] ids)
    {
        var index = $"lifecycle-{Guid.NewGuid():N}";
        ShouldSucceed(await Provider.EnsureIndexExistsAsync(index, Token));
        ShouldSucceed(await Indexing.IndexMultipleDocumentsAsync(ids.Select(id => Document(index, id)), Token));
        return index;
    }

    /// <summary>Starts a rebuild of <paramref name="index"/> and fills it with documents <paramref name="ids"/>.</summary>
    private async Task<IndexRebuild> FilledRebuildAsync(string index, params string[] ids)
    {
        var rebuild = await Lifecycle.StartRebuildAsync(index, Token);
        ShouldSucceed(rebuild);
        var indexed = await Lifecycle.IndexRebuildDocumentsAsync(rebuild.Value, ids.Select(id => Document(index, id)), Token);
        ShouldSucceed(indexed);
        indexed.Value.ShouldBe(ids.Length);
        return rebuild.Value;
    }

    private async Task DeleteAsync(string index, params IndexRebuild[] rebuilds)
    {
        try
        {
            await Provider.DeleteIndexAsync(index, CancellationToken.None);
            foreach (var rebuild in rebuilds)
            {
                await Lifecycle.AbortRebuildAsync(rebuild, CancellationToken.None);
            }
        }
        catch (Exception)
        {
            // Cleanup must not hide the test's own failure.
        }
    }

    [Fact]
    public async Task IndexPrefix_EveryOperation_UsesThePrefixedIndex()
    {
        var index = $"prefixed-{Guid.NewGuid():N}";
        try
        {
            var ensured = await Provider.EnsureIndexExistsAsync(index, Token);
            ShouldSucceed(ensured);
            ensured.Value.ShouldBeTrue();
            (await EngineIndexExistsAsync(Prefix + index)).ShouldBeTrue();
            (await EngineIndexExistsAsync(index)).ShouldBeFalse();

            var exists = await Provider.IndexExistsAsync(index, Token);
            ShouldSucceed(exists);
            exists.Value.ShouldBeTrue();

            ShouldSucceed(await Indexing.IndexDocumentAsync(Document(index, "a"), Token));
            ShouldSucceed(await Indexing.IndexMultipleDocumentsAsync([Document(index, "b"), Document(index, "c"), Document(index, "d")], Token));
            (await IdsAsync(index)).ShouldBe(["a", "b", "c", "d"]);
            // Nothing was written under the unprefixed name.
            (await EngineIndexExistsAsync(index)).ShouldBeFalse();

            ShouldSucceed(await Indexing.RemoveDocumentAsync(new DeleteDocumentRequest { Id = "a", Type = index }, Token));
            var removed = await Indexing.RemoveDocumentsAsync(index, ["b"], Token);
            ShouldSucceed(removed);
            removed.Value.ShouldBe(1);
            (await IdsAsync(index)).ShouldBe(["c", "d"]);

            var removedAll = await Indexing.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = index }, Token);
            ShouldSucceed(removedAll);
            removedAll.Value.ShouldBe(2);
            (await IdsAsync(index)).ShouldBeEmpty();

            var deleted = await Provider.DeleteIndexAsync(index, Token);
            ShouldSucceed(deleted);
            deleted.Value.ShouldBeTrue();
            (await EngineIndexExistsAsync(Prefix + index)).ShouldBeFalse();
            var existsAfterDelete = await Provider.IndexExistsAsync(index, Token);
            ShouldSucceed(existsAfterDelete);
            existsAfterDelete.Value.ShouldBeFalse();
        }
        finally
        {
            await DeleteAsync(index);
        }
    }

    [Fact]
    public async Task StartRebuild_Index_CreatesAPrefixedRebuildIndex()
    {
        var index = await IndexWithDocumentsAsync("old");
        IndexRebuild? rebuild = null;
        try
        {
            var started = await Lifecycle.StartRebuildAsync(index, Token);
            ShouldSucceed(started);
            rebuild = started.Value;

            rebuild.Index.ShouldBe(index);
            rebuild.TargetIndex.ShouldStartWith($"{Prefix}{index}-rebuild-");
            (await EngineIndexExistsAsync(rebuild.TargetIndex)).ShouldBeTrue();
        }
        finally
        {
            await DeleteAsync(index, rebuild!);
        }
    }

    [Fact]
    public async Task CompleteRebuild_AfterIndexing_SearchesSeeOldDocumentsUntilTheSwapAndNewOnesAfter()
    {
        var index = await IndexWithDocumentsAsync("old1", "old2");
        try
        {
            var rebuild = await FilledRebuildAsync(index, "new1", "new2", "new3");

            // The rebuild runs beside the live index, which searches still read.
            (await IdsAsync(index)).ShouldBe(["old1", "old2"]);

            var completed = await Lifecycle.CompleteRebuildAsync(rebuild, Token);
            ShouldSucceed(completed);

            (await IdsAsync(index)).ShouldBe(["new1", "new2", "new3"]);
            // The first rebuild replaces the plain index with an alias of the same name.
            completed.Value.ShouldBe([Prefix + index]);
            (await EngineIndexExistsAsync(Prefix + index)).ShouldBeFalse();
            (await AliasTargetsAsync(Prefix + index)).ShouldBe([rebuild.TargetIndex]);
        }
        finally
        {
            await DeleteAsync(index);
        }
    }

    [Fact]
    public async Task CompleteRebuild_SecondRebuild_DeletesOnlyThePreviousIndex()
    {
        var index = await IndexWithDocumentsAsync("old");
        try
        {
            var first = await FilledRebuildAsync(index, "first");
            ShouldSucceed(await Lifecycle.CompleteRebuildAsync(first, Token));
            var second = await FilledRebuildAsync(index, "second");

            var completed = await Lifecycle.CompleteRebuildAsync(second, Token);

            ShouldSucceed(completed);
            completed.Value.ShouldBe([first.TargetIndex]);
            (await EngineIndexExistsAsync(first.TargetIndex)).ShouldBeFalse();
            (await EngineIndexExistsAsync(second.TargetIndex)).ShouldBeTrue();
            (await AliasTargetsAsync(Prefix + index)).ShouldBe([second.TargetIndex]);
            (await IdsAsync(index)).ShouldBe(["second"]);

            // Writes through the index name go to the new index.
            ShouldSucceed(await Indexing.IndexDocumentAsync(Document(index, "later"), Token));
            (await IdsAsync(index)).ShouldBe(["later", "second"]);
        }
        finally
        {
            await DeleteAsync(index);
        }
    }

    [Fact]
    public async Task CompleteRebuild_CalledAgain_SucceedsAndDeletesNothing()
    {
        var index = await IndexWithDocumentsAsync("old");
        try
        {
            var rebuild = await FilledRebuildAsync(index, "new");
            ShouldSucceed(await Lifecycle.CompleteRebuildAsync(rebuild, Token));

            var again = await Lifecycle.CompleteRebuildAsync(rebuild, Token);

            ShouldSucceed(again);
            again.Value.ShouldBeEmpty();
            (await AliasTargetsAsync(Prefix + index)).ShouldBe([rebuild.TargetIndex]);
            (await IdsAsync(index)).ShouldBe(["new"]);
        }
        finally
        {
            await DeleteAsync(index);
        }
    }

    [Fact]
    public async Task AbortRebuild_Running_DeletesTheRebuildIndexAndKeepsTheAlias()
    {
        var index = await IndexWithDocumentsAsync("old");
        try
        {
            var first = await FilledRebuildAsync(index, "current");
            ShouldSucceed(await Lifecycle.CompleteRebuildAsync(first, Token));
            var second = await FilledRebuildAsync(index, "abandoned");

            var aborted = await Lifecycle.AbortRebuildAsync(second, Token);

            ShouldSucceed(aborted);
            aborted.Value.ShouldBeTrue();
            (await EngineIndexExistsAsync(second.TargetIndex)).ShouldBeFalse();
            (await AliasTargetsAsync(Prefix + index)).ShouldBe([first.TargetIndex]);
            (await IdsAsync(index)).ShouldBe(["current"]);
        }
        finally
        {
            await DeleteAsync(index);
        }
    }

    [Fact]
    public async Task CompleteRebuild_RebuildIndexGone_FailsAndLeavesTheAliasOnTheOldIndex()
    {
        var index = await IndexWithDocumentsAsync("old");
        try
        {
            var first = await FilledRebuildAsync(index, "current");
            ShouldSucceed(await Lifecycle.CompleteRebuildAsync(first, Token));
            var failed = await FilledRebuildAsync(index, "lost");
            ShouldSucceed(await Lifecycle.AbortRebuildAsync(failed, Token));

            var completed = await Lifecycle.CompleteRebuildAsync(failed, Token);

            completed.IsFailed.ShouldBeTrue();
            Errors(completed).ShouldContain(failed.TargetIndex);
            (await AliasTargetsAsync(Prefix + index)).ShouldBe([first.TargetIndex]);
            (await EngineIndexExistsAsync(first.TargetIndex)).ShouldBeTrue();
            (await IdsAsync(index)).ShouldBe(["current"]);
        }
        finally
        {
            await DeleteAsync(index);
        }
    }

    [Fact]
    public async Task CompleteRebuild_FirstRebuildIndexGone_FailsAndKeepsThePlainIndex()
    {
        var index = await IndexWithDocumentsAsync("old");
        try
        {
            var failed = await FilledRebuildAsync(index, "lost");
            ShouldSucceed(await Lifecycle.AbortRebuildAsync(failed, Token));

            var completed = await Lifecycle.CompleteRebuildAsync(failed, Token);

            completed.IsFailed.ShouldBeTrue();
            (await EngineIndexExistsAsync(Prefix + index)).ShouldBeTrue();
            (await IdsAsync(index)).ShouldBe(["old"]);
        }
        finally
        {
            await DeleteAsync(index);
        }
    }

    [Fact]
    public async Task AbortRebuild_Completed_FailsAndKeepsTheLiveIndex()
    {
        var index = await IndexWithDocumentsAsync("old");
        try
        {
            var rebuild = await FilledRebuildAsync(index, "new");
            ShouldSucceed(await Lifecycle.CompleteRebuildAsync(rebuild, Token));

            var aborted = await Lifecycle.AbortRebuildAsync(rebuild, Token);

            aborted.IsFailed.ShouldBeTrue();
            Errors(aborted).ShouldContain("already complete");
            (await EngineIndexExistsAsync(rebuild.TargetIndex)).ShouldBeTrue();
            (await IdsAsync(index)).ShouldBe(["new"]);
        }
        finally
        {
            await DeleteAsync(index);
        }
    }

    [Fact]
    public async Task CompleteRebuild_TargetNotARebuildOfTheIndex_FailsWithoutTouchingIt()
    {
        var index = await IndexWithDocumentsAsync("old");
        var other = await IndexWithDocumentsAsync("other");
        try
        {
            var forged = new IndexRebuild(index, Prefix + other);

            var completed = await Lifecycle.CompleteRebuildAsync(forged, Token);
            var aborted = await Lifecycle.AbortRebuildAsync(forged, Token);

            completed.IsFailed.ShouldBeTrue();
            Errors(completed).ShouldContain("is not a rebuild index of");
            aborted.IsFailed.ShouldBeTrue();
            Errors(aborted).ShouldContain("is not a rebuild index of");
            (await EngineIndexExistsAsync(Prefix + other)).ShouldBeTrue();
            (await EngineIndexExistsAsync(Prefix + index)).ShouldBeTrue();
        }
        finally
        {
            await DeleteAsync(index);
            await DeleteAsync(other);
        }
    }

    [Fact]
    public async Task IndexRebuildDocuments_DocumentForAnotherIndex_FailsAndIndexesNothing()
    {
        var index = await IndexWithDocumentsAsync("old");
        IndexRebuild? rebuild = null;
        try
        {
            rebuild = (await Lifecycle.StartRebuildAsync(index, Token)).Value;

            var indexed = await Lifecycle.IndexRebuildDocumentsAsync(rebuild, [Document(index, "ok"), Document("elsewhere", "stray")], Token);

            indexed.IsFailed.ShouldBeTrue();
            Errors(indexed).ShouldContain("Document stray is for index 'elsewhere'");
            ShouldSucceed(await Lifecycle.CompleteRebuildAsync(rebuild, Token));
            (await IdsAsync(index)).ShouldBeEmpty();
        }
        finally
        {
            await DeleteAsync(index);
        }
    }

    [Fact]
    public async Task DeleteIndex_AfterRebuild_DeletesTheIndexBehindTheAlias()
    {
        var index = await IndexWithDocumentsAsync("old");
        try
        {
            var rebuild = await FilledRebuildAsync(index, "new");
            ShouldSucceed(await Lifecycle.CompleteRebuildAsync(rebuild, Token));

            var deleted = await Provider.DeleteIndexAsync(index, Token);

            ShouldSucceed(deleted);
            (await EngineIndexExistsAsync(rebuild.TargetIndex)).ShouldBeFalse();
            (await AliasTargetsAsync(Prefix + index)).ShouldBeEmpty();
            var exists = await Provider.IndexExistsAsync(index, Token);
            ShouldSucceed(exists);
            exists.Value.ShouldBeFalse();
        }
        finally
        {
            await DeleteAsync(index);
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
