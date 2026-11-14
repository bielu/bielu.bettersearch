using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

/// <summary>
/// Zero-downtime rebuilds through <see cref="IIndexLifecycleServiceAsync"/>, with the index prefix tests of
/// <see cref="IndexPrefixTestBase"/>, inherited by the tests of every provider that implements
/// <see cref="IIndexLifecycleProviderAsync"/>. Each test checks what the engine holds by name, so a step that drops the
/// prefix or deletes the wrong index fails.
/// </summary>
/// <param name="services">
/// Services whose provider has <see cref="IndexPrefixTestBase.Prefix"/> as its index prefix and waits until writes are searchable.
/// </param>
public abstract class IndexLifecycleTestBase(IServiceProvider services) : IndexPrefixTestBase(services)
{
    /// <summary>The indexes the alias <paramref name="alias"/> points at on the engine, in ordinal order; empty when there is no such alias.</summary>
    protected abstract Task<IReadOnlyList<string>> AliasTargetsAsync(string alias);

    /// <summary>
    /// Does on the engine what an abort of <paramref name="rebuild"/> does before it deletes the new index, such as
    /// removing its pending alias, so the test can act as an overlapping abort that has not yet deleted it.
    /// </summary>
    protected abstract Task BeginAbortAsync(IndexRebuild rebuild);

    private IIndexLifecycleServiceAsync Lifecycle => ScopedServices.GetRequiredService<IIndexLifecycleServiceAsync>();

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
    public async Task CompleteRebuild_AbortStartedFirst_FailsAndLeavesTheAliasOnTheOldIndex()
    {
        var index = await IndexWithDocumentsAsync("old");
        try
        {
            var first = await FilledRebuildAsync(index, "current");
            ShouldSucceed(await Lifecycle.CompleteRebuildAsync(first, Token));
            var second = await FilledRebuildAsync(index, "abandoned");
            // An overlapping abort has claimed the rebuild but not yet deleted the index.
            await BeginAbortAsync(second);

            var completed = await Lifecycle.CompleteRebuildAsync(second, Token);

            completed.IsFailed.ShouldBeTrue();
            (await AliasTargetsAsync(Prefix + index)).ShouldBe([first.TargetIndex]);
            (await EngineIndexExistsAsync(second.TargetIndex)).ShouldBeTrue();
            (await IdsAsync(index)).ShouldBe(["current"]);

            var aborted = await Lifecycle.AbortRebuildAsync(second, Token);

            ShouldSucceed(aborted);
            aborted.Value.ShouldBeTrue();
            (await EngineIndexExistsAsync(second.TargetIndex)).ShouldBeFalse();
            (await AliasTargetsAsync(Prefix + index)).ShouldBe([first.TargetIndex]);
        }
        finally
        {
            await DeleteAsync(index);
        }
    }

    [Fact]
    public async Task AbortRebuild_AtTheSameTimeAsComplete_OneWinsAndTheAliasPointsAtAnIndexThatExists()
    {
        var index = await IndexWithDocumentsAsync("old");
        try
        {
            var first = await FilledRebuildAsync(index, "current");
            ShouldSucceed(await Lifecycle.CompleteRebuildAsync(first, Token));
            var second = await FilledRebuildAsync(index, "new");

            var completing = Lifecycle.CompleteRebuildAsync(second, Token);
            var aborting = Lifecycle.AbortRebuildAsync(second, Token);
            var completed = await completing;
            var aborted = await aborting;

            (completed.IsSuccess && aborted.IsSuccess).ShouldBeFalse();
            if (completed.IsSuccess)
            {
                Errors(aborted).ShouldContain("already complete");
                (await AliasTargetsAsync(Prefix + index)).ShouldBe([second.TargetIndex]);
                (await IdsAsync(index)).ShouldBe(["new"]);
            }
            else
            {
                ShouldSucceed(aborted);
                (await EngineIndexExistsAsync(second.TargetIndex)).ShouldBeFalse();
                (await AliasTargetsAsync(Prefix + index)).ShouldBe([first.TargetIndex]);
                (await IdsAsync(index)).ShouldBe(["current"]);
            }
        }
        finally
        {
            await DeleteAsync(index);
        }
    }

    [Fact]
    public async Task CompleteRebuild_TwoCallsAtOnce_BothSucceedAndTheNewIndexIsLive()
    {
        var index = await IndexWithDocumentsAsync("old");
        try
        {
            var rebuild = await FilledRebuildAsync(index, "new");

            var completed = await Task.WhenAll(Lifecycle.CompleteRebuildAsync(rebuild, Token), Lifecycle.CompleteRebuildAsync(rebuild, Token));

            ShouldSucceed(completed[0]);
            ShouldSucceed(completed[1]);
            (await AliasTargetsAsync(Prefix + index)).ShouldBe([rebuild.TargetIndex]);
            (await IdsAsync(index)).ShouldBe(["new"]);
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
}
