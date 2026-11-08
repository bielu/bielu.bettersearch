using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.FluentApi;
using Bielu.BetterSearch.FluentApi.Fluent.Extensions;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

public abstract class QueryResultBaseTests(
    ISearchServiceAsync serviceAsync,
    IIndexingServiceAsync indexingServiceAsync,
    IIndexingProviderAsync indexingProviderAsync)
{
    private async Task CreateExampleDocumentsAsync(string index)
    {
        var documents = new List<SearchDocument>
        {
            new SearchDocument()
            {
                Id = "1",
                Index = index,
                Fields = new Dictionary<string, IList<object>>()
                {
                    { "Name", ["Jane Doe"] },
                    { "Age", [25] }
                }
            },
            new SearchDocument()
            {
                Id = "2",
                Index = index,
                Fields = new Dictionary<string, IList<object>>()
                {
                    { "Name", ["John Doe 2"] },
                    { "Age", [30] }
                }
            },
            new SearchDocument()
            {
                Id = "3",
                Index = index,
                Fields = new Dictionary<string, IList<object>>()
                {
                    { "Name", ["John Doe 3"] },
                    { "Age", [35] }
                }
            }

        };

        foreach (var document in documents)
        {
            (await indexingServiceAsync.IndexDocumentAsync(document)).IsSuccess.ShouldBeTrue();
        }

        await DocumentsIndexedAsync(index);
    }

    /// <summary>Called once the example documents are indexed, for engines that must refresh before they can be searched.</summary>
    protected virtual Task DocumentsIndexedAsync(string index) => Task.CompletedTask;

    /// <summary>Whether hits carry the document's stored fields in <c>CustomProperties</c>.</summary>
    protected virtual bool ReturnsStoredFields => true;

    /// <summary>
    /// The ids of every document in <paramref name="index"/>. Searches with an empty query; override it for
    /// engines that cannot return every document.
    /// </summary>
    protected virtual async Task<IReadOnlyCollection<string>> IndexedIdsAsync(string index) =>
        await SearchIdsAsync(new SearchQuery { Index = index });

    [Fact]
    public async Task CanSearchWithPrefixQueryAsyncTest()
    {
        // An index of its own, so other tests can't change what it finds.
        var index = $"query-results-{Guid.NewGuid():N}";
        await CreateExampleDocumentsAsync(index);
        //Arrange
        var query = new SearchQuery();
        query.Index = index;
        query.Add(Occurance.MUST, new PrefixSubQuery()
        {
            Field = "Name",
            Value = "John"
        });
        //Act
        var result = await serviceAsync.SearchAsync(query);

        //Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Items.Select(item => item.Document.Id).Order(StringComparer.Ordinal).ShouldBe(["2", "3"]);
    }

    private Task<List<string>> SearchIdsAsync(SearchQuery query) => serviceAsync.SearchIdsAsync(query);

    private async Task<SearchQuery> CreateQueryAsync()
    {
        var index = $"query-results-{Guid.NewGuid():N}";
        await CreateExampleDocumentsAsync(index);
        return new SearchQuery { Index = index };
    }

    private static TermSubQuery Name(string value) => new() { Field = "Name", Value = value };

    [Fact]
    public async Task SearchAsync_Hits_CarryAScore()
    {
        var query = await CreateQueryAsync();
        query.Add(Occurance.MUST, Name("john"));

        var result = await serviceAsync.SearchAsync(query);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count().ShouldBe(2);
        result.Value.Items.ShouldAllBe(item => item.Score > 0);
        result.Value.Pagination.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task SearchAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var query = await CreateQueryAsync();
        query.Add(Occurance.MUST, Name("john"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => serviceAsync.SearchAsync(query, cancellation.Token));
    }

    [Fact]
    public async Task SearchAsync_CancelledTokenAndInvalidQuery_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // The query has no Index, which every provider rejects; the cancellation still wins.
        await Should.ThrowAsync<OperationCanceledException>(() => serviceAsync.SearchAsync(new SearchQuery(), cancellation.Token));
    }

    [Fact]
    public virtual async Task SearchAsync_MissingIndex_ReturnsFailedResult()
    {
        var index = $"missing-{Guid.NewGuid():N}";
        var query = new SearchQuery { Index = index };
        query.Add(Occurance.MUST, Name("john"));

        var result = await serviceAsync.SearchAsync(query);

        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.ShouldAllBe(error => !string.IsNullOrWhiteSpace(error.Message));
    }

    [Fact]
    public async Task SearchAsync_MixedCaseId_ComesBackUnchanged()
    {
        var index = $"query-results-{Guid.NewGuid():N}";
        (await indexingServiceAsync.IndexDocumentAsync(new SearchDocument
        {
            Id = "Up-1",
            Index = index,
            Fields = new Dictionary<string, IList<object>> { { "Name", ["Mixed Case"] } }
        })).IsSuccess.ShouldBeTrue();
        await DocumentsIndexedAsync(index);
        var query = new SearchQuery { Index = index };
        query.Add(Occurance.MUST, Name("mixed"));

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["Up-1"]);
    }

    [Fact]
    public async Task SearchAsync_StoredNullField_ReturnsTheHitWithAnEmptyField()
    {
        var index = $"query-results-{Guid.NewGuid():N}";
        // The service rejects a null field or value, so store it as another writer to the index would.
        (await indexingProviderAsync.IndexDocumentAsync(new SearchDocument
        {
            Id = "null-1",
            Index = index,
            Fields = new Dictionary<string, IList<object>>
            {
                { "Name", ["Null Field"] }, { "Tags", null! }, { "Labels", ["a", null!] }
            }
        })).IsSuccess.ShouldBeTrue();
        await DocumentsIndexedAsync(index);
        var query = new SearchQuery { Index = index };
        query.Add(Occurance.MUST, Name("null"));

        var result = await serviceAsync.SearchAsync(query);

        result.IsSuccess.ShouldBeTrue();
        var hit = result.Value.Items.ShouldHaveSingleItem();
        hit.Document.Id.ShouldBe("null-1");
        if (ReturnsStoredFields)
        {
            hit.Document.CustomProperties.ShouldContainKey("Tags");
            hit.Document.CustomProperties["Tags"].ShouldBeEmpty();
            hit.Document.CustomProperties["Labels"].ShouldBe(["a"]);
        }
        else
        {
            // Hits hold only the id, so the document is found by its other fields and carries none.
            hit.Document.CustomProperties.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task SearchAsync_StoredNullFields_ReturnsTheHitWithNoFields()
    {
        var index = $"query-results-{Guid.NewGuid():N}";
        // The service rejects null fields, so store the document as another writer to the index would.
        (await indexingProviderAsync.IndexDocumentAsync(new SearchDocument
        {
            Id = "null-fields-1",
            Index = index,
            Fields = null!
        })).IsSuccess.ShouldBeTrue();
        await DocumentsIndexedAsync(index);

        var result = await serviceAsync.SearchAsync(new SearchQuery { Index = index });

        result.IsSuccess.ShouldBeTrue();
        (await IndexedIdsAsync(index)).ShouldBe(["null-fields-1"]);
        if (ReturnsStoredFields)
        {
            var hit = result.Value.Items.ShouldHaveSingleItem();
            hit.Document.Id.ShouldBe("null-fields-1");
            hit.Document.CustomProperties.ShouldBeEmpty();
        }
    }

    [Fact]
    public virtual async Task SearchAsync_FluentCultureFilter_ReturnsOnlyThatCulture()
    {
        var ids = await SearchFluentCultureFilterAsync();

        ids.ShouldBe(["1"]);
    }

    /// <summary>
    /// Indexes three pages in three cultures and returns the ids a fluent en-US culture filter finds.
    /// Document 2's title holds the en-US culture value, so a filter that ignores its field matches it too.
    /// </summary>
    protected async Task<List<string>> SearchFluentCultureFilterAsync()
    {
        var index = $"query-results-{Guid.NewGuid():N}";
        SearchDocument Page(string id, string title, string culture) => new()
        {
            Id = id,
            Index = index,
            Fields = new Dictionary<string, IList<object>>
            {
                { "Title", [title] },
                { CultureField.Name, [CultureField.Value(culture)] }
            }
        };
        var pages = new[]
        {
            Page("1", "Welcome page", "en-US"),
            Page("2", $"Welcome page {CultureField.Value("en-US")}", "pl-PL"),
            Page("3", "Welcome page", "en-GB")
        };
        foreach (var page in pages)
        {
            (await indexingServiceAsync.IndexDocumentAsync(page)).IsSuccess.ShouldBeTrue();
        }

        await DocumentsIndexedAsync(index);
        var query = (SearchQuery)FluentApiSearchQueryCreator.Create()
            .WithSearchQuery(search => search.WithIndex(index))
            .CreateSearchQuery(search => search
                .CreateTermQuery(term => term.WithField("Title").WithValue("welcome"))
                .FilterByCulture("en-US"));

        return await SearchIdsAsync(query);
    }

    [Fact]
    public async Task SearchAsync_MustAndShould_ReturnsOnlyTheMustMatches()
    {
        var query = await CreateQueryAsync();
        query.Add(Occurance.SHOULD, Name("jane"));
        query.Add(Occurance.MUST, Name("john"));

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["2", "3"]);
    }

    [Fact]
    public async Task SearchAsync_ShouldOnly_ReturnsAnyMatch()
    {
        var query = await CreateQueryAsync();
        query.Add(Occurance.SHOULD, Name("jane"));
        query.Add(Occurance.SHOULD, Name("3"));

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["1", "3"]);
    }

    [Fact]
    public async Task SearchAsync_NestedBoolBuiltWithAdd_KeepsEachOccurrence()
    {
        var query = await CreateQueryAsync();
        var nested = new BoolSearchSubQuery();
        nested.Add(Occurance.SHOULD, Name("jane"));
        nested.Add(Occurance.MUST, Name("john"));
        nested.Add(Occurance.MUSTNOT, Name("3"));
        query.Add(Occurance.MUST, nested);

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["2"]);
    }

    /// <summary>The usual way to write an exclusion: a nested bool with only <c>MUSTNOT</c>, beside a positive clause.</summary>
    [Fact]
    public async Task SearchAsync_NestedMustNotOnlyBesideMust_ExcludesFromTheParentMatches()
    {
        var query = await CreateQueryAsync();
        var nested = new BoolSearchSubQuery();
        nested.Add(Occurance.MUSTNOT, Name("3"));
        query.Add(Occurance.MUST, Name("doe"));
        query.Add(Occurance.MUST, nested);

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["1", "2"]);
    }

    [Fact]
    public async Task SearchAsync_MustNotOfNestedMustNotOnly_RequiresTheNestedMatches()
    {
        var query = await CreateQueryAsync();
        var nested = new BoolSearchSubQuery();
        nested.Add(Occurance.MUSTNOT, Name("john"));
        query.Add(Occurance.MUST, Name("doe"));
        query.Add(Occurance.MUSTNOT, nested);

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["2", "3"]);
    }

    /// <summary>Excluding "not 3" on its own requires 3.</summary>
    [Fact]
    public async Task SearchAsync_OnlyMustNotOfNestedMustNotOnly_ReturnsTheNestedMatches()
    {
        var query = await CreateQueryAsync();
        var nested = new BoolSearchSubQuery();
        nested.Add(Occurance.MUSTNOT, Name("3"));
        query.Add(Occurance.MUSTNOT, nested);

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["3"]);
    }

    /// <summary>
    /// Lucene semantics: a <c>MUSTNOT</c> does not make <c>SHOULD</c> optional, so both must match. The
    /// <c>SHOULD</c> matches 2 and 3 and the double negation matches 1 and 3, so ignoring either one returns
    /// two documents instead of 3.
    /// </summary>
    [Fact]
    public async Task SearchAsync_ShouldBesideMustNotOfNestedMustNotOnly_ReturnsBothMatches()
    {
        var query = await CreateQueryAsync();
        var nested = new BoolSearchSubQuery();
        nested.Add(Occurance.MUSTNOT, Name("jane"));
        nested.Add(Occurance.MUSTNOT, Name("3"));
        query.Add(Occurance.SHOULD, Name("john"));
        query.Add(Occurance.MUSTNOT, nested);

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["3"]);
    }

    /// <summary>
    /// With nothing required, a <c>SHOULD</c> that only excludes still has to match: not jane (2 and 3) and
    /// the double negation (1 and 3) leave 3. Ignoring the <c>SHOULD</c> would return 1 and 3.
    /// </summary>
    [Fact]
    public async Task SearchAsync_ShouldOfNestedMustNotOnlyBesideDoubleNegation_ReturnsBothMatches()
    {
        var query = await CreateQueryAsync();
        var notJane = new BoolSearchSubQuery();
        notJane.Add(Occurance.MUSTNOT, Name("jane"));
        var doubleNegated = new BoolSearchSubQuery();
        doubleNegated.Add(Occurance.MUSTNOT, Name("jane"));
        doubleNegated.Add(Occurance.MUSTNOT, Name("3"));
        query.Add(Occurance.SHOULD, notJane);
        query.Add(Occurance.MUSTNOT, doubleNegated);

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["3"]);
    }

    /// <summary>
    /// A nested bool of <c>SHOULD</c> clauses where one only excludes: not john (1) or 3 gives 1 and 3,
    /// within the parent's doe.
    /// </summary>
    [Fact]
    public async Task SearchAsync_NestedShouldOfMustNotOnlyBesideShould_ReturnsEitherMatch()
    {
        var query = await CreateQueryAsync();
        var notJohn = new BoolSearchSubQuery();
        notJohn.Add(Occurance.MUSTNOT, Name("john"));
        var nested = new BoolSearchSubQuery();
        nested.Add(Occurance.SHOULD, notJohn);
        nested.Add(Occurance.SHOULD, Name("3"));
        query.Add(Occurance.MUST, Name("doe"));
        query.Add(Occurance.MUST, nested);

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["1", "3"]);
    }

    /// <summary>Lucene semantics: a <c>MUST</c> makes <c>SHOULD</c> optional, even when the <c>MUST</c> only excludes.</summary>
    [Fact]
    public virtual async Task SearchAsync_ShouldBesideMustOfNestedMustNotOnly_ReturnsEveryDocumentExceptTheExclusions()
    {
        var query = await CreateShouldBesideMustOfNestedMustNotOnlyQueryAsync();

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["1", "2"]);
    }

    /// <summary>For providers that cannot return the complement: a failed <c>Result</c>, never the <c>SHOULD</c> matches.</summary>
    protected async Task SearchAsync_ShouldBesideMustOfNestedMustNotOnly_ShouldFailAsync()
    {
        var query = await CreateShouldBesideMustOfNestedMustNotOnlyQueryAsync();

        var result = await serviceAsync.SearchAsync(query);

        result.IsFailed.ShouldBeTrue();
    }

    /// <summary>The same shape nested under a positive clause, which every provider can answer.</summary>
    [Fact]
    public async Task SearchAsync_NestedShouldBesideMustOfNestedMustNotOnly_ExcludesFromTheParentMatches()
    {
        var query = await CreateQueryAsync();
        var exclusion = new BoolSearchSubQuery();
        exclusion.Add(Occurance.MUSTNOT, Name("3"));
        var nested = new BoolSearchSubQuery();
        nested.Add(Occurance.SHOULD, Name("jane"));
        nested.Add(Occurance.MUST, exclusion);
        query.Add(Occurance.MUST, Name("doe"));
        query.Add(Occurance.MUST, nested);

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["1", "2"]);
    }

    private async Task<SearchQuery> CreateShouldBesideMustOfNestedMustNotOnlyQueryAsync()
    {
        var query = await CreateQueryAsync();
        var nested = new BoolSearchSubQuery();
        nested.Add(Occurance.MUSTNOT, Name("3"));
        query.Add(Occurance.SHOULD, Name("jane"));
        query.Add(Occurance.MUST, nested);
        return query;
    }

    /// <summary>Lucene semantics: <c>MUSTNOT</c> on its own returns every other document.</summary>
    [Fact]
    public virtual async Task SearchAsync_MustNotOnly_ReturnsTheComplement()
    {
        var query = await CreateQueryAsync();
        query.Add(Occurance.MUSTNOT, Name("jane"));

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["2", "3"]);
    }

    /// <summary>For providers that cannot return the complement: a failed <c>Result</c>, never the matches.</summary>
    protected async Task SearchAsync_MustNotOnly_ShouldFailAsync()
    {
        var query = await CreateQueryAsync();
        query.Add(Occurance.MUSTNOT, Name("jane"));

        var result = await serviceAsync.SearchAsync(query);

        result.IsFailed.ShouldBeTrue();
    }
}
