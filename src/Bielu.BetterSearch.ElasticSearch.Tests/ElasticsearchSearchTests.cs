using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Elastic.Clients.Elasticsearch;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

/// <summary>Runs queries against a real Elasticsearch and checks what comes back.</summary>
public sealed class ElasticsearchSearchTests(
    ISearchServiceAsync searchService,
    IIndexingProviderAsync indexingProvider,
    IClientFactoryAsync<ElasticsearchClient> clientFactory)
{
    private sealed class SortField : ISortOrderField
    {
        public string FieldName { get; set; } = string.Empty;
        public bool Desc { get; set; }
        public int Order { get; set; }
    }

    private static SearchDocument Person(string index, string id, string name, int age, long views, DateTime born, string city) => new()
    {
        Id = id,
        Type = "Person",
        Index = index,
        Fields = new Dictionary<string, IList<object>>
        {
            { "Name", [name] },
            { "Age", [age] },
            { "Views", [views] },
            { "Born", [born] },
            { "City", [city] }
        }
    };

    /// <summary>Indexes five people in an index of their own and returns its name.</summary>
    private async Task<string> CreatePeopleAsync()
    {
        var index = $"search-{Guid.NewGuid():N}";
        var people = new[]
        {
            Person(index, "Up-1", "Jane Doe", 25, 5_000_000_000, new DateTime(1999, 5, 1, 0, 0, 0, DateTimeKind.Utc), "Warsaw"),
            Person(index, "2", "John Doe", 30, 10, new DateTime(1994, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Krakow"),
            Person(index, "3", "John Smith", 35, 20, new DateTime(1989, 3, 1, 0, 0, 0, DateTimeKind.Utc), "Warsaw"),
            Person(index, "4", "Anna Nowak", 40, 30, new DateTime(1984, 7, 1, 0, 0, 0, DateTimeKind.Utc), "Gdansk"),
            Person(index, "5", "Piotr Jane", 45, 40, new DateTime(1979, 9, 1, 0, 0, 0, DateTimeKind.Utc), "Jane")
        };
        foreach (var person in people)
        {
            (await indexingProvider.IndexDocumentAsync(person)).IsSuccess.ShouldBeTrue();
        }

        await (await clientFactory.GetOrCreateClientAsync(index)).Indices.RefreshAsync(index);
        return index;
    }

    private async Task<List<string>> SearchIdsAsync(SearchQuery query)
    {
        query.PageSize = 100;
        var result = await searchService.SearchAsync(query);
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value.Items.Select(item => item.Document.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
    }

    private static SearchQuery Query(string index, Occurance occurance, ISearchSubQuery subQuery)
    {
        var query = new SearchQuery { Index = index };
        query.Add(occurance, subQuery);
        return query;
    }

    [Fact]
    public async Task SearchAsync_NoClauses_ReturnsEveryDocument()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(new SearchQuery { Index = index });

        ids.ShouldBe(["2", "3", "4", "5", "Up-1"]);
    }

    [Fact]
    public async Task SearchAsync_TermQuery_MatchesOnlyThatFieldIgnoringCase()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "Jane" }));

        // "Jane" is also a city, which a field-blind provider would match.
        ids.ShouldBe(["5", "Up-1"]);
    }

    [Fact]
    public async Task SearchAsync_TermsQuery_MatchesAnyTerm()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermsSubQuery { Field = "City", Value = ["warsaw", "Gdansk"] }));

        ids.ShouldBe(["3", "4", "Up-1"]);
    }

    [Fact]
    public async Task SearchAsync_TermsQueryOnKeywordField_MatchesTheValueAsGiven()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermsSubQuery { Field = "City.keyword", Value = ["Warsaw", "krakow"] }));

        ids.ShouldBe(["3", "Up-1"]);
    }

    [Fact]
    public async Task SearchAsync_TermsQueryWithManyValues_StaysUnderTheClauseLimit()
    {
        var index = await CreatePeopleAsync();
        // Above the default limit of 4,096 clauses that one term query per value would hit.
        var values = Enumerable.Range(0, 5_000).Select(i => (object)$"id-{i}").Append("gdansk").ToList();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermsSubQuery { Field = "City", Value = values }));

        ids.ShouldBe(["4"]);
    }

    [Fact]
    public async Task SearchAsync_TermsQueryWithNoValues_MatchesNothing()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermsSubQuery { Field = "City", Value = [] }));

        ids.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_TermsQueryPastTheTermsLimit_ReturnsFailedResult()
    {
        var index = await CreatePeopleAsync();
        // Each string is also sent lower-cased, so 32,769 distinct upper-case strings send 65,538 terms.
        var values = Enumerable.Range(0, 32_769).Select(i => (object)$"ID-{i}").ToList();

        var result = await searchService.SearchAsync(Query(index, Occurance.MUST, new TermsSubQuery { Field = "City", Value = values }), TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("index.max_terms_count");
    }

    [Fact]
    public async Task SearchAsync_PrefixQuery_MatchesPrefix()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new PrefixSubQuery { Field = "Name", Value = "Jo" }));

        ids.ShouldBe(["2", "3"]);
    }

    [Fact]
    public async Task SearchAsync_PrefixPhraseQuery_MatchesPhraseEndingInPrefix()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new PrefixPhraseSubQuery { Field = "Name", Value = "john sm" }));

        ids.ShouldBe(["3"]);
    }

    [Fact]
    public async Task SearchAsync_FuzzyQuery_MatchesWithinEditDistance()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new FuzzyQuery
        {
            Field = "Name",
            Value = "Smiht",
            Transposition = true,
            Fuziness = Tuple.Create(0, 1)
        }));

        ids.ShouldBe(["3"]);
    }

    [Fact]
    public async Task SearchAsync_FuzzyQueryOnKeywordField_KeepsTheValueCase()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new FuzzyQuery
        {
            Field = "City.keyword",
            Value = "Warsaw",
            Fuziness = Tuple.Create(0, 0)
        }));

        ids.ShouldBe(["3", "Up-1"]);
    }

    [Fact]
    public async Task SearchAsync_NumericRange_HonoursEdges()
    {
        var index = await CreatePeopleAsync();

        var inclusive = await SearchIdsAsync(Query(index, Occurance.MUST, new NumericRange
        {
            Field = "Age",
            MinValue = 30,
            MaxValue = 40,
            IncludeMinEdge = true,
            IncludeMaxEdge = true
        }));
        var exclusive = await SearchIdsAsync(Query(index, Occurance.MUST, new NumericRange
        {
            Field = "Age",
            MinValue = 30,
            MaxValue = 40
        }));

        inclusive.ShouldBe(["2", "3", "4"]);
        exclusive.ShouldBe(["3"]);
    }

    [Fact]
    public async Task SearchAsync_LongRange_MatchesValuesBeyondInt()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new LongRange
        {
            Field = "Views",
            MinValue = 4_000_000_000,
            IncludeMinEdge = true
        }));

        ids.ShouldBe(["Up-1"]);
    }

    [Fact]
    public async Task SearchAsync_DateRange_MatchesDatesInRange()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new DateRangeQuery
        {
            Field = "Born",
            MinValue = new DateTime(1985, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            MaxValue = new DateTime(1995, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        }));

        ids.ShouldBe(["2", "3"]);
    }

    [Fact]
    public async Task SearchAsync_StringRange_MatchesTermsInRange()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new StringRange
        {
            Field = "City.keyword",
            MinValue = "Gdansk",
            MaxValue = "Krakow",
            IncludeMinEdge = true,
            IncludeMaxEdge = true
        }));

        ids.ShouldBe(["2", "4", "5"]);
    }

    [Fact]
    public async Task SearchAsync_LuceneQuery_RunsRawSyntax()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new LuceneSubQuery { LuceneQuery = "fields.Name:john AND fields.City:warsaw" }));

        ids.ShouldBe(["3"]);
    }

    [Fact]
    public async Task SearchAsync_MustNotOnly_ReturnsTheComplement()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUSTNOT, new TermSubQuery { Field = "Name", Value = "john" }));

        ids.ShouldBe(["4", "5", "Up-1"]);
    }

    [Fact]
    public async Task SearchAsync_MustAndShould_ReturnsTheMustMatches()
    {
        var index = await CreatePeopleAsync();
        var query = Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "john" });
        query.Add(Occurance.SHOULD, new TermSubQuery { Field = "City", Value = "gdansk" });

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["2", "3"]);
    }

    [Fact]
    public async Task SearchAsync_ShouldOnly_ReturnsTheUnion()
    {
        var index = await CreatePeopleAsync();
        var query = Query(index, Occurance.SHOULD, new TermSubQuery { Field = "Name", Value = "anna" });
        query.Add(Occurance.SHOULD, new TermSubQuery { Field = "City", Value = "krakow" });

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["2", "4"]);
    }

    [Fact]
    public async Task SearchAsync_FilterOnly_MatchesWithoutScoring()
    {
        var index = await CreatePeopleAsync();

        var result = await searchService.SearchAsync(Query(index, Occurance.FILTER, new TermSubQuery { Field = "City", Value = "warsaw" }), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Select(item => item.Document.Id).OrderBy(id => id).ShouldBe(["3", "Up-1"]);
        result.Value.Items.ShouldAllBe(item => item.Score == 0);
    }

    [Fact]
    public async Task SearchAsync_NestedBoolQuery_KeepsEachClauseOccurrence()
    {
        var index = await CreatePeopleAsync();
        // City is Warsaw AND (Name is jane OR Age >= 35) AND NOT Name is doe
        var nested = new BoolSearchSubQuery();
        nested.Add(Occurance.SHOULD, new TermSubQuery { Field = "Name", Value = "jane" });
        nested.Add(Occurance.SHOULD, new NumericRange { Field = "Age", MinValue = 35, IncludeMinEdge = true });
        var query = Query(index, Occurance.FILTER, new TermSubQuery { Field = "City", Value = "warsaw" });
        query.Add(Occurance.MUST, nested);
        query.Add(Occurance.MUSTNOT, new TermSubQuery { Field = "Name", Value = "doe" });

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["3"]);
    }

    [Fact]
    public async Task SearchAsync_PageAndPageSize_ReturnThatPage()
    {
        var index = await CreatePeopleAsync();
        var query = new SearchQuery
        {
            Index = index,
            Page = 2,
            PageSize = 2,
            SortOrder = [new SortField { FieldName = "Age" }]
        };

        var result = await searchService.SearchAsync(query, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Select(item => item.Document.Id).ShouldBe(["3", "4"]);
        result.Value.Pagination.TotalCount.ShouldBe(5);
        result.Value.Pagination.Page.ShouldBe(2);
        result.Value.Pagination.PageSize.ShouldBe(2);
        result.Value.Pagination.TotalPages.ShouldBe(3);
    }

    [Theory]
    [InlineData(5_001, 2)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public async Task SearchAsync_PagePastTheResultWindow_ReturnsFailedResult(int page, int pageSize)
    {
        var index = await CreatePeopleAsync();

        var result = await searchService.SearchAsync(new SearchQuery { Index = index, Page = page, PageSize = pageSize }, TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("max_result_window");
    }

    [Fact]
    public async Task SearchAsync_LastPageInTheResultWindow_Succeeds()
    {
        var index = await CreatePeopleAsync();

        var result = await searchService.SearchAsync(new SearchQuery { Index = index, Page = 5_000, PageSize = 2 }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_FieldStartingWithUnderscore_IsADocumentField()
    {
        var index = await CreatePeopleAsync();
        var tagged = Person(index, "6", "Tag Holder", 50, 1, new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Lodz");
        tagged.Fields["_tags"] = ["blue"];
        (await indexingProvider.IndexDocumentAsync(tagged, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        await (await clientFactory.GetOrCreateClientAsync(index)).Indices.RefreshAsync(index, TestContext.Current.CancellationToken);

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "_tags", Value = "blue" }));

        ids.ShouldBe(["6"]);
    }

    [Fact]
    public async Task SearchAsync_SortByScoreMetadataField_PassesThrough()
    {
        var index = await CreatePeopleAsync();
        var query = Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "john" });
        query.SortOrder = [new SortField { FieldName = "_score", Desc = true }];

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["2", "3"]);
    }

    [Fact]
    public async Task SearchAsync_PageBeyondTheLast_ReturnsNoItemsWithTheTotal()
    {
        var index = await CreatePeopleAsync();

        var result = await searchService.SearchAsync(new SearchQuery { Index = index, Page = 10, PageSize = 2 }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
        result.Value.Pagination.TotalCount.ShouldBe(5);
    }

    [Fact]
    public async Task SearchAsync_SortOrder_SortsByEachFieldInOrder()
    {
        var index = await CreatePeopleAsync();
        var query = new SearchQuery
        {
            Index = index,
            PageSize = 100,
            SortOrder =
            [
                new SortField { FieldName = "Age", Desc = true, Order = 2 },
                new SortField { FieldName = "City.keyword", Order = 1 }
            ]
        };

        var result = await searchService.SearchAsync(query, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Select(item => item.Document.Id).ShouldBe(["4", "5", "2", "3", "Up-1"]);
    }

    [Fact]
    public async Task SearchAsync_Hit_CarriesIdScoreTypeAndFields()
    {
        var index = await CreatePeopleAsync();

        var result = await searchService.SearchAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "doe" }), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var hit = result.Value.Items.Single(item => item.Document.Id == "Up-1");
        var jane = hit.Document;
        hit.Score.ShouldNotBeNull();
        hit.Score.Value.ShouldBeGreaterThan(0);
        jane.ContentType.ShouldBe("Person");
        jane.CustomProperties["Name"].ShouldBe(["Jane Doe"]);
        jane.CustomProperties["Age"].ShouldBe([25L]);
        jane.CustomProperties["Views"].ShouldBe([5_000_000_000L]);
        jane.CustomProperties["Age"].Single().ShouldBeOfType<long>();
        jane.CustomProperties["City"].ShouldBe(["Warsaw"]);
    }

    /// <summary>
    /// Pins the JSON types: <c>_source</c> no longer knows the CLR type, so numbers written without a
    /// fraction come back as <c>long</c>, other numbers as <c>double</c>, dates as ISO 8601 strings and
    /// booleans as <c>bool</c>. The client writes a whole <c>double</c> as <c>3.0</c>, so it stays a <c>double</c>.
    /// </summary>
    [Fact]
    public async Task SearchAsync_HitFields_KeepTheirJsonTypes()
    {
        var index = $"search-{Guid.NewGuid():N}";
        var document = new SearchDocument
        {
            Id = "1",
            Index = index,
            Fields = new Dictionary<string, IList<object>>
            {
                { "Integer", [3] },
                { "Whole", [3.0d] },
                { "Fraction", [3.5d] },
                { "Born", [new DateTime(1999, 5, 1, 0, 0, 0, DateTimeKind.Utc)] },
                { "Active", [true] }
            }
        };
        (await indexingProvider.IndexDocumentAsync(document, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        await (await clientFactory.GetOrCreateClientAsync(index)).Indices.RefreshAsync(index, TestContext.Current.CancellationToken);

        var result = await searchService.SearchAsync(new SearchQuery { Index = index }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var fields = result.Value.Items.Single().Document.CustomProperties;
        fields["Integer"].Single().ShouldBeOfType<long>().ShouldBe(3L);
        fields["Whole"].Single().ShouldBeOfType<double>().ShouldBe(3.0d);
        fields["Fraction"].Single().ShouldBeOfType<double>().ShouldBe(3.5d);
        fields["Born"].Single().ShouldBeOfType<string>().ShouldBe("1999-05-01T00:00:00Z");
        fields["Active"].Single().ShouldBeOfType<bool>().ShouldBeTrue();
    }

    /// <summary>
    /// Indexes three dates into an index that keeps date strings as text, so <c>Born.keyword</c> holds the stored
    /// text as it is, and returns the index name.
    /// </summary>
    private async Task<string> CreateDatesAsTextAsync()
    {
        var index = $"search-{Guid.NewGuid():N}";
        var client = await clientFactory.GetOrCreateClientAsync(index);
        var created = await client.Indices.CreateAsync(new Elastic.Clients.Elasticsearch.IndexManagement.CreateIndexRequest(index)
        {
            Mappings = new Elastic.Clients.Elasticsearch.Mapping.TypeMapping { DateDetection = false }
        }, TestContext.Current.CancellationToken);
        created.IsValidResponse.ShouldBeTrue(created.DebugInformation);
        var documents = new[]
        {
            Dated(index, "milliseconds", new DateTime(2026, 11, 8, 13, 5, 0, 123, DateTimeKind.Unspecified)),
            Dated(index, "offset", new DateTimeOffset(2026, 11, 8, 15, 5, 0, TimeSpan.FromHours(1))),
            Dated(index, "minute-later", new DateTime(2026, 11, 8, 13, 6, 0, DateTimeKind.Utc))
        };
        (await indexingProvider.IndexMultipleDocumentsAsync(documents, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await client.Indices.RefreshAsync(index, TestContext.Current.CancellationToken)).IsValidResponse.ShouldBeTrue();
        return index;
    }

    private static SearchDocument Dated(string index, string id, object born) => new()
    {
        Id = id,
        Index = index,
        Fields = new Dictionary<string, IList<object>> { { "Born", [born] } }
    };

    [Fact]
    public async Task IndexAsync_Dates_AreStoredAsIsoTextInUtc()
    {
        var index = await CreateDatesAsTextAsync();

        var result = await searchService.SearchAsync(new SearchQuery { Index = index, PageSize = 10 }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
        result.Value.Items.ToDictionary(item => item.Document.Id, item => item.Document.CustomProperties["Born"].ShouldHaveSingleItem())
            .ShouldBe(new Dictionary<string, object>
            {
                { "milliseconds", "2026-11-08T13:05:00.123Z" },
                { "offset", "2026-11-08T14:05:00Z" },
                { "minute-later", "2026-11-08T13:06:00Z" }
            }, ignoreOrder: true);
    }

    [Fact]
    public async Task SearchAsync_PrefixOnKeywordWithDateTimeValue_MatchesTheStoredText()
    {
        var index = await CreateDatesAsTextAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new PrefixSubQuery
        {
            Field = "Born.keyword",
            Value = new DateTime(2026, 11, 8, 13, 5, 0, 123, DateTimeKind.Utc)
        }));

        ids.ShouldBe(["milliseconds"]);
    }

    [Fact]
    public async Task SearchAsync_PrefixOnKeywordWithDateTimeOffsetValue_MatchesTheSameInstant()
    {
        var index = await CreateDatesAsTextAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new PrefixSubQuery
        {
            Field = "Born.keyword",
            Value = new DateTimeOffset(2026, 11, 8, 14, 5, 0, TimeSpan.Zero)
        }));

        ids.ShouldBe(["offset"]);
    }

    [Fact]
    public async Task SearchAsync_FuzzyWithDateValue_ReturnsFailedResult()
    {
        var index = await CreatePeopleAsync();

        var result = await searchService.SearchAsync(Query(index, Occurance.MUST, new FuzzyQuery
        {
            Field = "Born",
            Value = new DateTime(1999, 5, 1, 0, 0, 0, DateTimeKind.Utc)
        }), TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(error => error.Message.Contains("a fuzzy match needs a string or a number"));
    }

    [Fact]
    public async Task SearchAsync_Properties_ReturnsOnlyThoseFields()
    {
        var index = await CreatePeopleAsync();
        var query = Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "anna" });
        query.Properties = ["Name"];

        var result = await searchService.SearchAsync(query, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var anna = result.Value.Items.Single().Document;
        anna.Id.ShouldBe("4");
        anna.ContentType.ShouldBe("Person");
        anna.CustomProperties.Keys.ShouldBe(["Name"]);
    }

    [Fact]
    public async Task SearchAsync_PostFilter_FiltersHitsButNotTheQuery()
    {
        var index = await CreatePeopleAsync();
        var query = Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "john" });
        query.PostFilterQuery[Occurance.MUST] = [Nested(new TermSubQuery { Field = "City", Value = "krakow" })];

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["2"]);
    }

    private static BoolSearchSubQuery Nested(ISearchSubQuery subQuery)
    {
        var nested = new BoolSearchSubQuery();
        nested.Add(Occurance.MUST, subQuery);
        return nested;
    }

    [Fact]
    public async Task SearchAsync_NoMatches_ReturnsEmptyResult()
    {
        var index = await CreatePeopleAsync();

        var result = await searchService.SearchAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "nobody" }), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
        result.Value.Pagination.TotalCount.ShouldBe(0);
        result.Value.Pagination.TotalPages.ShouldBe(0);
    }

    [Fact]
    public async Task SearchAsync_MissingIndex_ReturnsFailedResult()
    {
        var result = await searchService.SearchAsync(new SearchQuery { Index = $"missing-{Guid.NewGuid():N}" }, TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("index_not_found_exception");
    }

    [Fact]
    public async Task SearchAsync_InvalidLuceneSyntax_ReturnsFailedResult()
    {
        var index = await CreatePeopleAsync();

        var result = await searchService.SearchAsync(Query(index, Occurance.MUST, new LuceneSubQuery { LuceneQuery = "fields.Name:(john" }), TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("Elasticsearch search failed");
    }

    [Fact]
    public async Task SearchAsync_NoIndex_ReturnsFailedResult()
    {
        var result = await searchService.SearchAsync(new SearchQuery(), TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("Index");
    }

    [Fact]
    public async Task SearchAsync_SubQueryWithoutField_ReturnsFailedResult()
    {
        var result = await searchService.SearchAsync(Query("any", Occurance.MUST, new TermSubQuery { Value = "jane" }), TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("Field");
    }

    [Fact]
    public async Task SearchAsync_Facets_ReturnsUnsupportedFailure()
    {
        var query = new SearchQuery { Index = "any" };
        query.FacetQueries.Add(new TermAggregation { AggregationName = "cities", TermFieldName = "City" });

        var result = await searchService.SearchAsync(query, TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("facets");
    }
}
