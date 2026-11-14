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

    /// <summary>Whether the provider supports range queries. Without them, each range test checks for a failed <c>Result</c> instead.</summary>
    protected virtual bool SupportsRanges => true;

    /// <summary>
    /// Runs <paramref name="test"/> on a query over an index of its own holding one document per value of
    /// <paramref name="field"/>, with ids from 1, and deletes the index afterwards.
    /// </summary>
    private async Task WithRangeQueryAsync(string field, object[] values, Func<SearchQuery, Task> test)
    {
        var index = $"query-results-{Guid.NewGuid():N}";
        try
        {
            for (var i = 0; i < values.Length; i++)
            {
                (await indexingServiceAsync.IndexDocumentAsync(new SearchDocument
                {
                    Id = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Index = index,
                    Fields = new Dictionary<string, IList<object>> { { "Name", ["Ranged"] }, { field, [values[i]] } }
                }, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
            }

            await DocumentsIndexedAsync(index);
            await test(new SearchQuery { Index = index });
        }
        finally
        {
            try
            {
                await indexingProviderAsync.DeleteIndexAsync(index, CancellationToken.None);
            }
            catch (Exception)
            {
                // Cleanup must not hide the test's own failure.
            }
        }
    }

    /// <summary>The ids matching <paramref name="query"/>, or <c>null</c> once a provider without ranges has failed it.</summary>
    private async Task<List<string>?> SearchRangeIdsAsync(SearchQuery query)
    {
        if (SupportsRanges)
        {
            return await serviceAsync.SearchIdsAsync(query, TestContext.Current.CancellationToken);
        }

        var result = await serviceAsync.SearchAsync(query, TestContext.Current.CancellationToken);
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(error => error.Message.Contains("does not support") && error.Message.Contains("range queries"));
        return null;
    }

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
    public async Task SearchAsync_DocumentWithNullFields_ReturnsTheHitWithNoFields()
    {
        var index = $"query-results-{Guid.NewGuid():N}";
        // SearchDocument turns null fields into an empty dictionary, so this stores and reads back a document with no fields.
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
    public async Task SearchAsync_StoredValues_ComeBackAsTheSameClrTypes()
    {
        var index = $"query-results-{Guid.NewGuid():N}";
        (await indexingServiceAsync.IndexDocumentAsync(new SearchDocument
        {
            Id = "types-1",
            Index = index,
            Fields = new Dictionary<string, IList<object>>
            {
                { "Name", ["Typed Values"] },
                { "Age", [25] },
                { "Views", [5_000_000_000L] },
                { "Rating", [4.5] },
                { "Score", [3.0] },
                { "Active", [true] }
            }
        })).IsSuccess.ShouldBeTrue();
        await DocumentsIndexedAsync(index);
        var query = new SearchQuery { Index = index };
        query.Add(Occurance.MUST, Name("typed"));

        var result = await serviceAsync.SearchAsync(query);

        result.IsSuccess.ShouldBeTrue();
        var hit = result.Value.Items.ShouldHaveSingleItem();
        hit.Document.Id.ShouldBe("types-1");
        if (ReturnsStoredFields)
        {
            var fields = hit.Document.CustomProperties;
            fields["Name"].ShouldHaveSingleItem().ShouldBeOfType<string>().ShouldBe("Typed Values");
            fields["Age"].ShouldHaveSingleItem().ShouldBeOfType<long>().ShouldBe(25L);
            fields["Views"].ShouldHaveSingleItem().ShouldBeOfType<long>().ShouldBe(5_000_000_000L);
            fields["Rating"].ShouldHaveSingleItem().ShouldBeOfType<double>().ShouldBe(4.5);
            fields["Score"].ShouldHaveSingleItem().ShouldBeOfType<double>().ShouldBe(3.0);
            fields["Active"].ShouldHaveSingleItem().ShouldBeOfType<bool>().ShouldBeTrue();
        }
    }

    /// <summary>
    /// Dates are stored as the text a query sends for them: ISO 8601 in UTC, a date without a time zone taken as UTC,
    /// and fractional seconds only when they are not zero.
    /// </summary>
    [Fact]
    public async Task SearchAsync_StoredDates_ComeBackAsIsoTextInUtc()
    {
        var index = $"query-results-{Guid.NewGuid():N}";
        (await indexingServiceAsync.IndexDocumentAsync(new SearchDocument
        {
            Id = "dates-1",
            Index = index,
            Fields = new Dictionary<string, IList<object>>
            {
                { "Name", ["Dated Values"] },
                { "Unspecified", [new DateTime(2026, 11, 8, 13, 5, 0, 123, DateTimeKind.Unspecified)] },
                { "Utc", [new DateTime(2026, 11, 8, 13, 5, 0, DateTimeKind.Utc)] },
                { "Offset", [new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.FromHours(1))] },
                { "Day", [new DateOnly(2026, 11, 8)] }
            }
        }, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        await DocumentsIndexedAsync(index);
        var query = new SearchQuery { Index = index };
        query.Add(Occurance.MUST, Name("dated"));

        var result = await serviceAsync.SearchAsync(query, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        var hit = result.Value.Items.ShouldHaveSingleItem();
        hit.Document.Id.ShouldBe("dates-1");
        if (ReturnsStoredFields)
        {
            var fields = hit.Document.CustomProperties;
            fields["Unspecified"].ShouldHaveSingleItem().ShouldBe("2026-11-08T13:05:00.123Z");
            fields["Utc"].ShouldHaveSingleItem().ShouldBe("2026-11-08T13:05:00Z");
            fields["Offset"].ShouldHaveSingleItem().ShouldBe("2026-11-07T23:00:00Z");
            fields["Day"].ShouldHaveSingleItem().ShouldBe("2026-11-08");
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

    /// <summary>A sub-query put in <c>NestedQueries</c> without <c>Add</c> keeps its default occurrence, <c>MUST</c>.</summary>
    [Fact]
    public async Task SearchAsync_SubQueryAddedToNestedQueriesDirectly_IsAMust()
    {
        var query = await CreateQueryAsync();
        var nested = new BoolSearchSubQuery
        {
            NestedQueries = [Name("john"), new TermSubQuery { Field = "Name", Value = "jane", Occurance = Occurance.SHOULD }]
        };
        query.Add(Occurance.MUST, nested);

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["2", "3"]);
    }

    /// <summary>Both edges are inclusive by default: the ages 25 and 30 sit exactly on the bounds.</summary>
    [Fact]
    public virtual async Task SearchAsync_RangeWithDefaultEdges_IncludesBothBounds()
    {
        var query = await CreateQueryAsync();
        query.Add(Occurance.MUST, new NumericRange { Field = "Age", MinValue = 25, MaxValue = 30 });

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["1", "2"]);
    }

    /// <summary>
    /// A fractional range keeps its bounds exactly: 9.98 and 49.51 lie just outside, 9.99 and 49.5 sit on the edges,
    /// so a bound rounded or cut to a whole number, or an edge moved by a precision error, returns other documents.
    /// </summary>
    [Theory]
    [InlineData(true, new[] { "2", "3", "4" })]
    [InlineData(false, new[] { "3" })]
    public Task SearchAsync_FractionalRange_MatchesOnlyValuesInsideTheBounds(bool includeEdges, string[] expected) =>
        WithRangeQueryAsync("Price", [9.98, 9.99, 20.25, 49.5, 49.51], async query =>
        {
            query.Add(Occurance.MUST, new NumericRange
            {
                Field = "Price",
                MinValue = 9.99,
                MaxValue = 49.5,
                IncludeMinEdge = includeEdges,
                IncludeMaxEdge = includeEdges
            });

            (await SearchRangeIdsAsync(query))?.ShouldBe(expected);
        });

    /// <summary>A fractional bound on whole numbers is not rounded: at least 4.5 is 5, at most 4.5 is 4.</summary>
    [Theory]
    [InlineData(4.5, null, "2")]
    [InlineData(null, 4.5, "1")]
    public Task SearchAsync_FractionalBoundOnWholeNumbers_IsNotRounded(double? min, double? max, string expected) =>
        WithRangeQueryAsync("Rating", [4, 5], async query =>
        {
            query.Add(Occurance.MUST, new NumericRange { Field = "Rating", MinValue = min, MaxValue = max });

            (await SearchRangeIdsAsync(query))?.ShouldBe([expected]);
        });

    /// <summary>
    /// A <see cref="DateTimeOffset"/> bound compares instants: the documents are at 10:00, 11:00 and 12:00 UTC, written
    /// with other offsets, and the range runs from 10:30 to 12:00 UTC, also written with other offsets. Reading the
    /// clock time of either side as UTC matches all three.
    /// </summary>
    [Theory]
    [InlineData(true, new[] { "2", "3" })]
    [InlineData(false, new[] { "2" })]
    public Task SearchAsync_DateRangeWithOffsets_ComparesInstants(bool includeMaxEdge, string[] expected) =>
        WithRangeQueryAsync("Born",
        [
            new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.FromHours(2)),
            new DateTime(2026, 11, 8, 11, 0, 0, DateTimeKind.Utc),
            new DateTimeOffset(2026, 11, 8, 7, 0, 0, TimeSpan.FromHours(-5))
        ], async query =>
        {
            query.Add(Occurance.MUST, new DateRangeQuery
            {
                Field = "Born",
                MinValue = new DateTimeOffset(2026, 11, 8, 6, 30, 0, TimeSpan.FromHours(-4)),
                MaxValue = new DateTimeOffset(2026, 11, 8, 13, 0, 0, TimeSpan.FromHours(1)),
                IncludeMaxEdge = includeMaxEdge
            });

            (await SearchRangeIdsAsync(query))?.ShouldBe(expected);
        });

    /// <summary>A <see cref="DateTime"/> bound with <see cref="DateTimeKind.Unspecified"/> is read as UTC, whatever the machine's time zone.</summary>
    [Fact]
    public Task SearchAsync_DateRangeWithUnspecifiedKind_ReadsTheBoundAsUtc() =>
        WithRangeQueryAsync("Born",
        [
            new DateTime(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 8, 11, 0, 0, DateTimeKind.Utc)
        ], async query =>
        {
            query.Add(Occurance.MUST, new DateRangeQuery
            {
                Field = "Born",
                MinValue = new DateTime(2026, 11, 8, 10, 30, 0, DateTimeKind.Unspecified),
                MaxValue = new DateTime(2026, 11, 8, 11, 30, 0, DateTimeKind.Unspecified)
            });

            (await SearchRangeIdsAsync(query))?.ShouldBe(["2"]);
        });

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

    /// <summary>For providers that do not support ranges: a failed <c>Result</c>, never the matches.</summary>
    protected async Task SearchAsync_RangeWithDefaultEdges_ShouldFailAsync()
    {
        var query = await CreateQueryAsync();
        query.Add(Occurance.MUST, new NumericRange { Field = "Age", MinValue = 25, MaxValue = 30 });

        var result = await serviceAsync.SearchAsync(query);

        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(error => error.Message.Contains("does not support numeric range queries"));
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
