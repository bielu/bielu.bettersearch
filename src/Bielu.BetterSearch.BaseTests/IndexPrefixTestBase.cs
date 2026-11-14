using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

/// <summary>
/// The index prefix, inherited by the tests of every provider that has one. The test checks what the engine holds by
/// name, so a step that drops the prefix fails.
/// </summary>
/// <param name="services">Services whose provider has <see cref="Prefix"/> as its index prefix and waits until writes are searchable.</param>
public abstract class IndexPrefixTestBase(IServiceProvider services) : IDisposable
{
    private readonly IServiceScope _scope = services.CreateScope();

    /// <summary>The services the tests run on.</summary>
    protected IServiceProvider Services { get; } = services;

    /// <summary>The index prefix the services are configured with.</summary>
    protected abstract string Prefix { get; }

    /// <summary>The field the test counts a term facet on: <c>Name</c>, or a keyword form of it on an engine that needs one.</summary>
    protected virtual string FacetField => "Name";

    /// <summary>Whether the provider counts term facets, which the test then counts too.</summary>
    protected virtual bool HasFacets => true;

    /// <summary>Whether the engine has an index, not an alias, named exactly <paramref name="engineName"/>.</summary>
    protected abstract Task<bool> EngineIndexExistsAsync(string engineName);

    protected static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The services of the scope the tests run in.</summary>
    protected IServiceProvider ScopedServices => _scope.ServiceProvider;

    protected IIndexingServiceAsync Indexing => _scope.ServiceProvider.GetRequiredService<IIndexingServiceAsync>();
    protected IIndexingProviderAsync Provider => _scope.ServiceProvider.GetRequiredService<IIndexingProviderAsync>();
    protected ISearchServiceAsync Search => _scope.ServiceProvider.GetRequiredService<ISearchServiceAsync>();

    protected static SearchDocument Document(string index, string id) => new()
    {
        Id = id,
        Type = "TestDocument",
        Index = index,
        Fields = new Dictionary<string, IList<object>> { { "Name", [$"item {id}"] } }
    };

    /// <summary>The ids of every document in <paramref name="index"/>, each named <c>item</c> and its id.</summary>
    protected Task<List<string>> IdsAsync(string index) =>
        Search.SearchIdsAsync(SearchTestHelpers.Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "item" }), Token);

    protected static void ShouldSucceed(ResultBase result) =>
        result.IsSuccess.ShouldBeTrue(Errors(result));

    protected static string Errors(ResultBase result) => string.Join("; ", result.Errors.Select(error => error.Message));

    /// <summary>Deletes <paramref name="index"/>, never failing, so cleanup can't hide the test's own failure.</summary>
    protected async Task DeleteIndexAsync(string index)
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
            if (HasFacets)
            {
                var faceted = await Search.SearchAsync(new SearchQuery
                {
                    Index = index,
                    FacetQueries = [new TermAggregation { AggregationName = "names", TermFieldName = FacetField }]
                }, Token);
                ShouldSucceed(faceted);
                faceted.Value.RetrieveBucketAggregation("names").Buckets.Cast<KeyBucket>().Select(bucket => bucket.Key).Order()
                    .ShouldBe(["item a", "item b", "item c", "item d"]);
            }

            await GetCountAndUpdateAsync(index);

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
            await DeleteIndexAsync(index);
        }
    }

    private async Task GetCountAndUpdateAsync(string index)
    {
        var got = await Search.GetDocumentAsync(index, "a", Token);
        ShouldSucceed(got);
        got.Value.ShouldNotBeNull().Fields["Name"].ShouldHaveSingleItem().ShouldBe("item a");
        var counted = await Search.CountAsync(SearchTestHelpers.Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "item" }), Token);
        ShouldSucceed(counted);
        counted.Value.ShouldBe(4);
        var updated = await Indexing.UpdateDocumentAsync(index, "d", new Dictionary<string, IList<object>> { ["Name"] = ["item d updated"] }, Token);
        ShouldSucceed(updated);
        updated.Value.ShouldBeTrue();
        var gotUpdated = await Search.GetDocumentAsync(index, "d", Token);
        ShouldSucceed(gotUpdated);
        gotUpdated.Value.ShouldNotBeNull().Fields["Name"].ShouldHaveSingleItem().ShouldBe("item d updated");
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
