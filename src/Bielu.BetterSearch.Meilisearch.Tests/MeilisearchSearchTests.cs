using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Meilisearch.Services;
using Bielu.BetterSearch.Tests;
using Microsoft.Extensions.Options;
using Shouldly;
using MeilisearchClient = Meilisearch.MeilisearchClient;
using MeilisearchQuery = Meilisearch.SearchQuery;

namespace Bielu.BetterSearch.Meilisearch.Tests;

/// <summary>Runs queries against a real Meilisearch and checks what comes back.</summary>
public sealed class MeilisearchSearchTests(
    ISearchServiceAsync searchService,
    IIndexingProviderAsync indexingProvider,
    IQueryTranslateServiceAsync<MeilisearchQuery> translateService,
    IOptionsMonitor<MeilisearchOptions> options,
    IClientFactoryAsync<MeilisearchClient> clientFactory)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

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
        (await indexingProvider.IndexMultipleDocumentsAsync(people, Token)).Value.ShouldBe(people.Length);
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
    public async Task SearchAsync_TermQueryWithNonAsciiLetters_IgnoresCase()
    {
        var index = $"search-{Guid.NewGuid():N}";
        var turtle = new SearchDocument
        {
            Id = "1",
            Index = index,
            Fields = new Dictionary<string, IList<object>> { { "Name", ["Żółw Café"] } }
        };
        (await indexingProvider.IndexDocumentAsync(turtle, Token)).IsSuccess.ShouldBeTrue();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "żółw" }));

        ids.ShouldBe(["1"]);
    }

    [Fact]
    public async Task SearchAsync_TermQueryOnStoredValue_MatchesTheWholeValue()
    {
        var index = await CreatePeopleAsync();

        var whole = await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "fields.Name", Value = "John Doe" }));
        var word = await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "fields.Name", Value = "John" }));

        whole.ShouldBe(["2"]);
        word.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_TermQueryWithNumber_MatchesTheValue()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Age", Value = 35 }));

        ids.ShouldBe(["3"]);
    }

    [Fact]
    public async Task SearchAsync_TermQueryWithDate_MatchesTheDate()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Born", Value = new DateTime(1994, 1, 1, 0, 0, 0, DateTimeKind.Utc) }));

        ids.ShouldBe(["2"]);
    }

    [Fact]
    public async Task SearchAsync_TermsQuery_MatchesAnyTerm()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermsSubQuery { Field = "City", Value = ["warsaw", "Gdansk"] }));

        ids.ShouldBe(["3", "4", "Up-1"]);
    }

    [Fact]
    public async Task SearchAsync_TermsQueryWithNoValues_MatchesNothing()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermsSubQuery { Field = "City", Value = [] }));

        ids.ShouldBeEmpty();
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
        var middle = await SearchIdsAsync(Query(index, Occurance.MUST, new PrefixPhraseSubQuery { Field = "Name", Value = "Do" }));

        ids.ShouldBe(["3"]);
        middle.ShouldBe(["2", "Up-1"]);
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
            MaxValue = 40,
            IncludeMinEdge = false,
            IncludeMaxEdge = false
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
            Field = "fields.City",
            MinValue = "Gdansk",
            MaxValue = "Krakow",
            IncludeMinEdge = true,
            IncludeMaxEdge = true
        }));

        ids.ShouldBe(["2", "4", "5"]);
    }

    [Fact]
    public async Task SearchAsync_StringRangeOnWords_NeedsOneWordInsideTheRange()
    {
        var index = await CreatePeopleAsync();

        // "Jane Doe" has "jane" past the minimum and "doe" before the maximum, but neither word inside the range.
        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new StringRange
        {
            Field = "Name",
            MinValue = "e",
            MaxValue = "i",
            IncludeMinEdge = true,
            IncludeMaxEdge = true
        }));

        ids.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_StringRange_HonoursExcludedEdges()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new StringRange
        {
            Field = "fields.City",
            MinValue = "Gdansk",
            MaxValue = "Warsaw",
            IncludeMinEdge = false,
            IncludeMaxEdge = false
        }));

        ids.ShouldBe(["2", "5"]);
    }

    [Fact]
    public async Task SearchAsync_NumericRangeOnManyValues_NeedsOneValueInsideTheRange()
    {
        var index = $"search-{Guid.NewGuid():N}";
        SearchDocument Scores(string id, params object[] scores) => new()
        {
            Id = id,
            Index = index,
            Fields = new Dictionary<string, IList<object>> { { "Scores", scores } }
        };
        (await indexingProvider.IndexMultipleDocumentsAsync([Scores("1", 0, 10), Scores("2", 3), Scores("3", 5)], Token)).IsSuccess.ShouldBeTrue();

        var inclusive = await SearchIdsAsync(Query(index, Occurance.MUST, new NumericRange
        {
            Field = "Scores",
            MinValue = 1,
            MaxValue = 5,
            IncludeMinEdge = true,
            IncludeMaxEdge = true
        }));
        var exclusive = await SearchIdsAsync(Query(index, Occurance.MUST, new NumericRange { Field = "Scores", MinValue = 1, MaxValue = 5, IncludeMinEdge = false, IncludeMaxEdge = false }));

        inclusive.ShouldBe(["2", "3"]);
        exclusive.ShouldBe(["2"]);
    }

    [Fact]
    public async Task SearchAsync_NumberWithPositiveExponent_MatchesTheValue()
    {
        // .NET writes 1e20 as 1E+20, which a Meilisearch filter rejects; the provider sends 1E20.
        var index = $"search-{Guid.NewGuid():N}";
        SearchDocument Amount(string id, double amount) => new()
        {
            Id = id,
            Index = index,
            Fields = new Dictionary<string, IList<object>> { { "Amount", [amount] } }
        };
        try
        {
            (await indexingProvider.IndexMultipleDocumentsAsync([Amount("1", 1e20), Amount("2", 1e19)], Token)).Value.ShouldBe(2);

            var term = await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Amount", Value = 1e20 }));
            var terms = await SearchIdsAsync(Query(index, Occurance.MUST, new TermsSubQuery { Field = "Amount", Value = [1e20, 1e21] }));

            term.ShouldBe(["1"]);
            terms.ShouldBe(["1"]);
        }
        finally
        {
            try
            {
                await indexingProvider.DeleteIndexAsync(index, CancellationToken.None);
            }
            catch (Exception)
            {
                // Cleanup must not hide the test's own failure.
            }
        }
    }

    [Fact]
    public async Task RawFilter_NonFiniteAndExponentPlusLiterals_AreReadAsTextOrRejected()
    {
        // Pins the engine behavior behind MeilisearchFieldNames.Number and RequireFinite: a filter reads NaN and
        // Infinity as strings, and rejects 1E+20.
        var index = $"search-{Guid.NewGuid():N}";
        SearchDocument Amount(string id, object amount) => new()
        {
            Id = id,
            Index = index,
            Fields = new Dictionary<string, IList<object>> { { "Amount", [amount] } }
        };
        try
        {
            (await indexingProvider.IndexMultipleDocumentsAsync(
                [Amount("1", 1e20), Amount("2", "NaN"), Amount("3", "Infinity"), Amount("4", 3.5)], Token)).Value.ShouldBe(4);
            var client = await clientFactory.GetOrCreateClientAsync(index);

            async Task<List<string>> FilterIdsAsync(string filter) =>
                (await client.Index(index).SearchAsync<SearchDocument>(string.Empty, new MeilisearchQuery { Filter = filter }, Token))
                .Hits.Select(hit => hit.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();

            (await FilterIdsAsync("fields.Amount = NaN")).ShouldBe(["2"]);
            (await FilterIdsAsync("fields.Amount = Infinity")).ShouldBe(["3"]);
            (await FilterIdsAsync("fields.Amount = 1E20")).ShouldBe(["1"]);
            var rejected = await Should.ThrowAsync<global::Meilisearch.MeilisearchApiError>(() => FilterIdsAsync("fields.Amount = 1E+20"));
            rejected.Message.ShouldContain("+20");
        }
        finally
        {
            try
            {
                await indexingProvider.DeleteIndexAsync(index, CancellationToken.None);
            }
            catch (Exception)
            {
                // Cleanup must not hide the test's own failure.
            }
        }
    }

    [Fact]
    public async Task SearchAsync_TermQueryOnStoredValue_IgnoresCase()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "fields.City", Value = "WARSAW" }));

        ids.ShouldBe(["3", "Up-1"]);
    }

    [Theory]
    [InlineData("Age", "30", "2")]
    [InlineData("Age", "30.0", "2")]
    [InlineData("fields.Age", "30", "2")]
    [InlineData("Active", "true", "2")]
    public async Task SearchAsync_StringTermOnNumberOrBoolean_MatchesTheValue(string field, string value, string expected)
    {
        var index = await CreatePeopleAsync();
        (await indexingProvider.IndexDocumentAsync(new SearchDocument
        {
            Id = "2",
            Index = index,
            Fields = new Dictionary<string, IList<object>> { { "Age", [30] }, { "Active", [true] } }
        }, Token)).IsSuccess.ShouldBeTrue();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = field, Value = value }));

        ids.ShouldBe([expected]);
    }

    [Fact]
    public async Task SearchAsync_DateTermOnStoredValue_MatchesTheDate()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "fields.Born", Value = new DateTime(1994, 1, 1, 0, 0, 0, DateTimeKind.Utc) }));

        ids.ShouldBe(["2"]);
    }

    [Fact]
    public async Task SearchAsync_TermsQueryWithManyValues_MatchesAnyOfThem()
    {
        var index = await CreatePeopleAsync();
        var values = Enumerable.Range(0, 2_000).Select(i => (object)$"city{i}").Append("Gdansk").ToList();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermsSubQuery { Field = "City", Value = values }));

        ids.ShouldBe(["4"]);
    }

    [Fact]
    public async Task SearchAsync_MustNotOnly_ReturnsTheComplement()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUSTNOT, new TermSubQuery { Field = "Name", Value = "john" }));

        ids.ShouldBe(["4", "5", "Up-1"]);
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
            SortOrder = [new SortOrderField { FieldName = "Age" }]
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
    public async Task SearchAsync_PagePastMaxTotalHits_ReturnsFailedResult(int page, int pageSize)
    {
        var result = await searchService.SearchAsync(new SearchQuery { Index = "any", Page = page, PageSize = pageSize }, TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("MaxTotalHits");
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
                new SortOrderField { FieldName = "Age", Desc = true, Order = 2 },
                new SortOrderField { FieldName = "City", Order = 1 }
            ]
        };

        var result = await searchService.SearchAsync(query, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Select(item => item.Document.Id).ShouldBe(["4", "5", "2", "3", "Up-1"]);
    }

    [Fact]
    public async Task SearchAsync_SortByInvalidField_ReturnsFailedResult()
    {
        var index = await CreatePeopleAsync();

        var result = await searchService.SearchAsync(new SearchQuery { Index = index, SortOrder = [new SortOrderField { FieldName = "Age desc" }] }, TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
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
        jane.CustomProperties.Keys.OrderBy(key => key).ShouldBe(["Age", "Born", "City", "Name", "Views"]);
        jane.CustomProperties["Name"].ShouldBe(["Jane Doe"]);
        jane.CustomProperties["Age"].ShouldBe([25L]);
        jane.CustomProperties["Views"].ShouldBe([5_000_000_000L]);
        jane.CustomProperties["City"].ShouldBe(["Warsaw"]);
    }

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
                { "Fraction", [3.5d] },
                { "Born", [new DateTime(1999, 5, 1, 0, 0, 0, DateTimeKind.Utc)] },
                { "Active", [true] },
                { "Nested", [new Dictionary<string, object> { ["A"] = 1 }] }
            }
        };
        (await indexingProvider.IndexDocumentAsync(document, Token)).IsSuccess.ShouldBeTrue();

        var result = await searchService.SearchAsync(new SearchQuery { Index = index }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var fields = result.Value.Items.Single().Document.CustomProperties;
        fields["Integer"].Single().ShouldBeOfType<long>().ShouldBe(3L);
        fields["Fraction"].Single().ShouldBeOfType<double>().ShouldBe(3.5d);
        fields["Born"].Single().ShouldBeOfType<string>().ShouldBe("1999-05-01T00:00:00Z");
        fields["Active"].Single().ShouldBeOfType<bool>().ShouldBeTrue();
        fields["Nested"].Single().ShouldBeOfType<System.Text.Json.JsonElement>().GetProperty("A").GetInt32().ShouldBe(1);
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

    /// <summary>Warsaw, Krakow and Gdansk, the first two as strings and the last as a dictionary.</summary>
    private async Task<string> CreatePlacesAsync()
    {
        var index = $"places-{Guid.NewGuid():N}";
        SearchDocument Place(string id, object location) => new()
        {
            Id = id,
            Type = "Place",
            Index = index,
            Fields = new Dictionary<string, IList<object>> { ["Location"] = [location] }
        };

        var places = new[]
        {
            Place("warsaw", "52.2297,21.0122"),
            Place("krakow", "50.0647,19.9450"),
            Place("gdansk", new Dictionary<string, object> { ["lat"] = 54.3520, ["lon"] = 18.6466 }),
            new SearchDocument { Id = "nowhere", Type = "Place", Index = index }
        };
        (await indexingProvider.IndexMultipleDocumentsAsync(places, Token)).Value.ShouldBe(places.Length);
        return index;
    }

    [Fact]
    public async Task SearchAsync_SpatialQuery_MatchesDocumentsWithinTheDistance()
    {
        var index = await CreatePlacesAsync();

        // Lodz is about 120 km from Warsaw, 190 km from Krakow and 290 km from Gdansk.
        SpatialSearchQuery Around(double distance, string? unit = null) => new()
        {
            Field = "Location",
            Lat = 51.7592,
            Lng = 19.4560,
            Distance = distance,
            UnitOfDistance = unit
        };
        var near = await SearchIdsAsync(Query(index, Occurance.MUST, Around(150)));
        var wider = await SearchIdsAsync(Query(index, Occurance.MUST, Around(250)));
        var miles = await SearchIdsAsync(Query(index, Occurance.MUST, Around(200, "mi")));
        var outside = await SearchIdsAsync(Query(index, Occurance.MUSTNOT, Around(150)));

        near.ShouldBe(["warsaw"]);
        wider.ShouldBe(["krakow", "warsaw"]);
        miles.ShouldBe(["gdansk", "krakow", "warsaw"]);
        outside.ShouldBe(["gdansk", "krakow", "nowhere"]);
    }

    [Fact]
    public async Task SearchAsync_DocumentWithLocation_ReturnsTheLocationAsIndexed()
    {
        var index = await CreatePlacesAsync();

        var result = await searchService.SearchAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "fields.Location", Value = "52.2297,21.0122" }), TestContext.Current.CancellationToken);

        result.Value.Items.Single().Document.CustomProperties["Location"].ShouldBe(["52.2297,21.0122"]);
    }

    [Fact]
    public async Task SearchAsync_PostFilter_FiltersHits()
    {
        var index = await CreatePeopleAsync();
        var query = Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "john" });
        var nested = new BoolSearchSubQuery();
        nested.Add(Occurance.MUST, new TermSubQuery { Field = "City", Value = "krakow" });
        query.PostFilterQuery[Occurance.MUST] = [nested];

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["2"]);
    }

    [Fact]
    public async Task SearchAsync_DocumentsWithSettingsAppliedOnCreate_AreFilterable()
    {
        var index = $"search-{Guid.NewGuid():N}";
        (await indexingProvider.CreateIndexAsync(index, Token)).Value.ShouldBeTrue();
        (await indexingProvider.IndexDocumentAsync(Person(index, "1", "Ola", 20, 1, DateTime.UnixEpoch, "Lodz"), Token)).IsSuccess.ShouldBeTrue();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "City", Value = "lodz" }));

        ids.ShouldBe(["1"]);
    }

    [Fact]
    public async Task SearchAsync_IndexRecreatedAfterDelete_IsFilterableAgain()
    {
        var index = await CreatePeopleAsync();
        (await indexingProvider.DeleteIndexAsync(index, Token)).Value.ShouldBeTrue();
        (await indexingProvider.IndexDocumentAsync(Person(index, "1", "Ola", 20, 1, DateTime.UnixEpoch, "Lodz"), Token)).IsSuccess.ShouldBeTrue();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "City", Value = "lodz" }));

        ids.ShouldBe(["1"]);
    }

    [Fact]
    public async Task SearchAsync_NoMatches_ReturnsEmptyResult()
    {
        var index = await CreatePeopleAsync();

        var result = await searchService.SearchAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "nobody" }), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
        result.Value.Pagination.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task SearchAsync_MissingIndex_ReturnsFailedResultWithTheCode()
    {
        var result = await searchService.SearchAsync(new SearchQuery { Index = $"missing-{Guid.NewGuid():N}" }, TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Metadata["code"].ShouldBe("index_not_found");
    }

    [Fact]
    public async Task SearchAsync_NoIndex_ReturnsFailedResult()
    {
        var result = await searchService.SearchAsync(new SearchQuery(), TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task SearchAsync_SubQueryWithoutField_ReturnsFailedResult()
    {
        var index = await CreatePeopleAsync();

        var result = await searchService.SearchAsync(Query(index, Occurance.MUST, new TermSubQuery { Value = "jane" }), TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task SearchAsync_Facets_ReturnsUnsupportedFailure()
    {
        var result = await searchService.SearchAsync(new SearchQuery { Index = "any", FacetQueries = [new TermAggregation()] }, TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task SearchAsync_UnreachableServer_ReturnsFailedResult()
    {
        var monitor = new TestOptionsMonitor<MeilisearchOptions>(new MeilisearchOptions
        {
            SharedIndexSettings = new MeilisearchIndexSettings { ConnectionString = "http://127.0.0.1:1" },
        });
        var provider = new MeilisearchSearchProviderAsync(translateService,
            new MeilisearchClientFactory(new MeilisearchClientSettingsManager(monitor)), monitor);

        var result = await provider.SearchAsync(new SearchQuery { Index = "any" }, TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
    }

    [Fact]
    public void MaxTotalHits_Default_MatchesTheElasticsearchWindow()
    {
        options.CurrentValue.MaxTotalHits.ShouldBe(10_000);
    }

    public static TheoryData<string, string[]> LuceneQueries => new()
    {
        { "Name:jane", ["5", "Up-1"] },
        { "fields.Name:JANE", ["5", "Up-1"] },
        { "Name:jo*", ["2", "3"] },
        { "Name:jo* AND NOT City:warsaw", ["2"] },
        { "Name:jane OR City:krakow", ["2", "5", "Up-1"] },
        { "+City:warsaw Name:jane", ["3", "Up-1"] },
        { "-City:warsaw", ["2", "4", "5"] },
        { "Age:[30 TO 40]", ["2", "3", "4"] },
        { "Age:{30 TO 40}", ["3"] },
        { "Age:[40 TO *]", ["4", "5"] },
        { "Views:[5000000000 TO *]", ["Up-1"] },
        { "City:[a TO h]", ["4"] },
        // A date without a time covers the whole day, as on Elasticsearch.
        { "Born:[1989-03-01 TO 1994-01-01]", ["2", "3"] },
        { "Born:{1989-03-01 TO *]", ["2", "Up-1"] },
        { "Born:[* TO 1984-07-01T00:00:00Z]", ["4", "5"] },
        { "_exists_:City", ["2", "3", "4", "5", "Up-1"] },
        { "Nickname:*", [] },
    };

    [Theory]
    [MemberData(nameof(LuceneQueries))]
    public async Task SearchAsync_LuceneQuery_MatchesAsElasticsearchWould(string lucene, string[] expected)
    {
        var index = await CreatePeopleAsync();
        var query = Query(index, Occurance.MUST, new LuceneSubQuery { LuceneQuery = lucene });
        query.PageSize = 100;

        var result = await searchService.SearchAsync(query, Token);

        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
        result.Value.Items.Select(item => item.Document.Id).OrderBy(id => id, StringComparer.Ordinal).ShouldBe(expected);
        result.Successes.OfType<TranslationWarning>().ShouldBeEmpty();
    }

    /// <summary>The ranges Elasticsearch's own test pins, so a date without a time rounds as it does there.</summary>
    [Theory]
    [MemberData(nameof(LuceneDateRangeCases.Ranges), MemberType = typeof(LuceneDateRangeCases))]
    public async Task SearchAsync_LuceneDateRangeWithoutTime_RoundsAsElasticsearch(string lucene, string[] expected)
    {
        var index = $"search-{Guid.NewGuid():N}";
        try
        {
            var documents = LuceneDateRangeCases.Documents(index);
            (await indexingProvider.IndexMultipleDocumentsAsync(documents, Token)).Value.ShouldBe(documents.Length);

            var result = await searchService.SearchAsync(Query(index, Occurance.MUST, new LuceneSubQuery { LuceneQuery = lucene }), Token);

            result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
            result.Value.Items.Select(item => item.Document.Id).OrderBy(id => id, StringComparer.Ordinal).ShouldBe(expected);
        }
        finally
        {
            try
            {
                await indexingProvider.DeleteIndexAsync(index, CancellationToken.None);
            }
            catch (Exception)
            {
                // Cleanup must not hide the test's own failure.
            }
        }
    }

    [Theory]
    [InlineData("Name:\"Jane-Doe\"", new[] { "Up-1" })]
    [InlineData("Name:\"John Doe\"", new[] { "2" })]
    [InlineData("Name:\"jane DOE\"", new[] { "Up-1" })]
    // The last word must be a whole word of the field: "jane do" is not the start of "jane doe".
    [InlineData("Name:\"Jane Do\"", new string[0])]
    [InlineData("Name:\"doe john\"", new string[0])]
    public async Task SearchAsync_LucenePhrase_MatchesTheWordsInOrderWithWarning(string lucene, string[] expected)
    {
        var index = await CreatePeopleAsync();

        var result = await searchService.SearchAsync(Query(index, Occurance.MUST, new LuceneSubQuery { LuceneQuery = lucene }), Token);

        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
        result.Value.Items.Select(item => item.Document.Id).OrderBy(id => id, StringComparer.Ordinal).ShouldBe(expected);
        result.Successes.OfType<TranslationWarning>().Single().Message.ShouldContain("matches a run of words starting with it");
    }

    [Fact]
    public async Task SearchAsync_LuceneFuzzyTerm_MatchesTheExactWordWithWarning()
    {
        var index = await CreatePeopleAsync();

        var exact = await searchService.SearchAsync(Query(index, Occurance.MUST, new LuceneSubQuery { LuceneQuery = "Name:jane~" }), Token);
        var typo = await searchService.SearchAsync(Query(index, Occurance.MUST, new LuceneSubQuery { LuceneQuery = "Name:jnae~" }), Token);

        exact.Value.Items.Select(item => item.Document.Id).OrderBy(id => id, StringComparer.Ordinal).ShouldBe(["5", "Up-1"]);
        exact.Successes.OfType<TranslationWarning>().Single().Message.ShouldContain("matches the exact word");
        typo.Value.Items.ShouldBeEmpty();
        typo.Successes.OfType<TranslationWarning>().Single().Message.ShouldContain("matches the exact word");
    }

    [Fact]
    public async Task SearchAsync_LuceneEmptyPhrase_ReturnsFailure()
    {
        var index = await CreatePeopleAsync();

        var result = await searchService.SearchAsync(Query(index, Occurance.MUST, new LuceneSubQuery { LuceneQuery = "Name:jane AND Name:\"\"" }), Token);

        result.Errors.Single().Message.ShouldContain("empty phrase");
    }

    [Fact]
    public async Task SearchAsync_LuceneTermWithoutField_ReturnsFailure()
    {
        var index = await CreatePeopleAsync();

        var result = await searchService.SearchAsync(Query(index, Occurance.MUST, new LuceneSubQuery { LuceneQuery = "jane" }), Token);

        result.Errors.Single().Message.ShouldContain("names no field");
    }
}
