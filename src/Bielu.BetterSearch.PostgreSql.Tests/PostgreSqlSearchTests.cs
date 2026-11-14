using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.PostgreSql.Services;
using Bielu.BetterSearch.Tests;
using Npgsql;
using Shouldly;

namespace Bielu.BetterSearch.PostgreSql.Tests;

/// <summary>Query behavior against a real PostgreSQL that the shared test bases don't cover.</summary>
public sealed class PostgreSqlSearchTests(
    ISearchServiceAsync searchService,
    IIndexingServiceAsync indexingService,
    IIndexingProviderAsync indexingProvider,
    PostgreSqlResource postgres)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SearchDocument Document(string index, string id, Dictionary<string, IList<object>> fields) =>
        new() { Id = id, Type = "Item", Index = index, Fields = fields };

    /// <summary>Indexes <paramref name="documents"/> into an index of its own, runs <paramref name="test"/> and deletes the index.</summary>
    private async Task WithIndexAsync(Func<string, IEnumerable<SearchDocument>> documents, Func<string, Task> test)
    {
        var index = $"pg-search-{Guid.NewGuid():N}";
        try
        {
            var indexed = await indexingService.IndexMultipleDocumentsAsync(documents(index).ToList(), Token);
            indexed.IsSuccess.ShouldBeTrue(string.Join("; ", indexed.Errors.Select(error => error.Message)));
            await test(index);
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

    private Task<List<string>> IdsAsync(string index, ISearchSubQuery subQuery, Occurance occurance = Occurance.MUST) =>
        searchService.SearchIdsAsync(SearchTestHelpers.Query(index, occurance, subQuery), Token);

    [Theory]
    [InlineData("Name'); DROP TABLE people; --")]
    [InlineData("Na\"me")]
    [InlineData("$1")]
    [InlineData("fields -> 'x'")]
    public async Task SearchAsync_HostileFieldNameAndValue_MatchesThatFieldOnly(string field)
    {
        const string value = "x' OR '1'='1";
        await WithIndexAsync(index =>
        [
            Document(index, "1", new() { { field, [value] } }),
            Document(index, "2", new() { { "Other", [value] } }),
        ], async index =>
        {
            (await IdsAsync(index, new TermSubQuery { Field = field, Value = "x" })).ShouldBe(["1"]);
            (await IdsAsync(index, new TermsSubQuery { Field = field, Value = [value, "1"] })).ShouldBe(["1"]);
            (await IdsAsync(index, new PrefixSubQuery { Field = field, Value = "x'" })).ShouldBe(["1"]);
            (await IdsAsync(index, new StringRange { Field = field, MinValue = value, MaxValue = value })).ShouldBe(["1"]);
            (await indexingProvider.IndexExistsAsync(index, Token)).Value.ShouldBeTrue();
        });
    }

    [Fact]
    public async Task SearchAsync_HostileIndexName_UsesItsOwnTable()
    {
        var index = $"pg\"; DROP TABLE x; --{Guid.NewGuid():N}";
        try
        {
            (await indexingService.IndexDocumentAsync(Document(index, "1", new() { { "Name", ["Jane"] } }), Token)).IsSuccess.ShouldBeTrue();

            (await searchService.SearchIdsAsync(new SearchQuery { Index = index }, Token)).ShouldBe(["1"]);
            (await indexingProvider.DeleteIndexAsync(index, Token)).Value.ShouldBeTrue();
            (await indexingProvider.IndexExistsAsync(index, Token)).Value.ShouldBeFalse();
        }
        finally
        {
            await indexingProvider.DeleteIndexAsync(index, CancellationToken.None);
        }
    }

    [Fact]
    public async Task IndexDocumentAsync_IndexNameLongerThanPostgreSqlKeeps_FailsNamingTheLimit()
    {
        var result = await indexingProvider.IndexDocumentAsync(Document(new string('i', 64), "1", new() { { "Name", ["Jane"] } }), Token);

        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("longer than 63 bytes");
    }

    [Fact]
    public async Task SearchAsync_StoredValues_RoundTripAndMatchExactly()
    {
        const long beyondDouble = 9_007_199_254_740_993; // 2^53 + 1, which a double can't hold.
        await WithIndexAsync(index =>
        [
            Document(index, "1", new()
            {
                { "Big", [beyondDouble] }, { "Whole", [3.0] }, { "Tiny", [1e-300] }, { "Huge", [1e300] }, { "Large", [1e15] }, { "Exact", [2.5m] }
            }),
            Document(index, "2", new() { { "Big", [beyondDouble - 1] }, { "Whole", [3.5] } }),
        ], async index =>
        {
            var document = (await searchService.GetDocumentAsync(index, "1", Token)).Value.ShouldNotBeNull();
            document.Fields["Big"].ShouldHaveSingleItem().ShouldBeOfType<long>().ShouldBe(beyondDouble);
            document.Fields["Whole"].ShouldHaveSingleItem().ShouldBeOfType<double>().ShouldBe(3.0);
            document.Fields["Tiny"].ShouldHaveSingleItem().ShouldBeOfType<double>().ShouldBe(1e-300);
            document.Fields["Huge"].ShouldHaveSingleItem().ShouldBeOfType<double>().ShouldBe(1e300);
            document.Fields["Large"].ShouldHaveSingleItem().ShouldBeOfType<double>().ShouldBe(1e15);
            document.Fields["Exact"].ShouldHaveSingleItem().ShouldBeOfType<double>().ShouldBe(2.5);

            (await IdsAsync(index, new TermSubQuery { Field = "Big", Value = beyondDouble })).ShouldBe(["1"]);
            (await IdsAsync(index, new TermSubQuery { Field = "Big", Value = "9007199254740993" })).ShouldBe(["1"]);
            (await IdsAsync(index, new LongRange { Field = "Big", MinValue = beyondDouble })).ShouldBe(["1"]);
            (await IdsAsync(index, new TermSubQuery { Field = "Whole", Value = 3 })).ShouldBe(["1"]);
            (await IdsAsync(index, new TermSubQuery { Field = "Whole", Value = "3.5" })).ShouldBe(["2"]);
        });
    }

    [Fact]
    public async Task SearchAsync_NonFiniteNumber_FailsNamingTheField()
    {
        var result = await indexingProvider.IndexDocumentAsync(Document($"pg-nan-{Guid.NewGuid():N}", "1", new() { { "Rating", [double.NaN] } }), Token);

        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("Field Rating holds NaN");
    }

    [Fact]
    public async Task SearchAsync_NumericStringBeyondLong_MatchesTheExactStoredInteger()
    {
        await WithIndexAsync(index =>
        [
            Document(index, "1", new() { { "Big", [ulong.MaxValue] } }),
            // ulong.MaxValue + 1, which a double rounds ulong.MaxValue to.
            Document(index, "2", new() { { "Big", [18446744073709551616m] } }),
        ], async index =>
        {
            (await IdsAsync(index, new TermSubQuery { Field = "Big", Value = "18446744073709551615" })).ShouldBe(["1"]);
            (await IdsAsync(index, new TermSubQuery { Field = "Big", Value = "18446744073709551616" })).ShouldBe(["2"]);
        });
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_StructuredValues_KeepTheirContent()
    {
        await WithIndexAsync(index =>
        [
            Document(index, "1", new()
            {
                { "Nested", [new Dictionary<string, object> { ["tags"] = new[] { "red", "blue" } }] },
                { "Item", [new Dimensions(2, "cm")] },
            }),
        ], async index =>
        {
            var document = (await searchService.GetDocumentAsync(index, "1", Token)).Value.ShouldNotBeNull();
            var nested = document.Fields["Nested"].ShouldHaveSingleItem().ShouldBeOfType<System.Text.Json.JsonElement>();
            nested.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()).ShouldBe(["red", "blue"]);
            var item = document.Fields["Item"].ShouldHaveSingleItem().ShouldBeOfType<System.Text.Json.JsonElement>();
            item.GetProperty("Width").GetInt32().ShouldBe(2);
            item.GetProperty("Unit").GetString().ShouldBe("cm");
            // The strings inside are words of the document, as other string values are.
            (await IdsAsync(index, new TermSubQuery { Field = "Nested", Value = "blue" })).ShouldBe(["1"]);
        });
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_OneDocumentCannotBeWritten_ReportsEveryIdAsNotIndexed()
    {
        var index = $"pg-failed-{Guid.NewGuid():N}";
        try
        {
            var result = await indexingProvider.IndexMultipleDocumentsAsync(
            [
                Document(index, "1", new() { { "Rating", [1.5] } }),
                Document(index, "2", new() { { "Rating", [double.NaN] } }),
                Document(index, "3", new() { { "Rating", [2.5] } }),
            ], Token);

            var error = result.Errors.ShouldHaveSingleItem();
            error.Message.ShouldContain("document 2: Field Rating holds NaN");
            error.Metadata["ids"].ShouldBe(new List<string> { "1", "2", "3" });
            (await indexingProvider.IndexExistsAsync(index, Token)).Value.ShouldBeFalse();
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
    public async Task IndexAndUpdate_DefaultJsonElement_FailInsteadOfThrowing()
    {
        await WithIndexAsync(index => [Document(index, "1", new() { { "Name", ["Jane"] } })], async index =>
        {
            var indexed = await indexingProvider.IndexDocumentAsync(Document(index, "2", new() { { "Value", [default(System.Text.Json.JsonElement)] } }), Token);
            var updated = await indexingProvider.UpdateDocumentAsync(index, "1", new Dictionary<string, IList<object>> { { "Value", [default(System.Text.Json.JsonElement)] } }, Token);

            indexed.Errors.ShouldHaveSingleItem().Message.ShouldContain("Field Value holds a JsonElement with no value");
            updated.Errors.ShouldHaveSingleItem().Message.ShouldContain("Field Value holds a JsonElement with no value");
            (await searchService.SearchIdsAsync(new SearchQuery { Index = index }, Token)).ShouldBe(["1"]);
        });
    }

    [Fact]
    public async Task SearchAsync_DateOnlyValueInASessionOutsideUtc_IsTakenAsUtc()
    {
        var index = $"pg-zone-{Guid.NewGuid():N}";
        // Europe/Warsaw is ahead of UTC, so a date read in it starts an hour before the UTC midnight.
        var builder = new NpgsqlConnectionStringBuilder(postgres.EnsureStarted()) { Timezone = "Europe/Warsaw" };
        var options = new TestOptionsMonitor<Configuration.PostgreSqlOptions>(new Configuration.PostgreSqlOptions
        {
            SharedIndexSettings = new Configuration.PostgreSqlIndexSettings { ConnectionString = builder.ConnectionString },
        });
        using var factory = new PostgreSqlDataSourceFactory(new PostgreSqlClientSettingsManager(options), new EmptyServiceProvider());
        var indexing = new PostgreSqlIndexingProviderAsync(factory, options,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PostgreSqlIndexingProviderAsync>.Instance);
        var search = new PostgreSqlSearchProviderAsync(
            new PostgreSqlQueryTranslateService([new Services.Queries.DateRangeSubQueryTranslator()]), factory, options);
        try
        {
            var indexed = await indexing.IndexMultipleDocumentsAsync(
            [
                Document(index, "1", new() { { "Day", [new DateOnly(2026, 1, 2)] } }),
                Document(index, "2", new() { { "Day", ["2026-01-02T00:30:00+01:00"] } }),
            ], Token);
            indexed.IsSuccess.ShouldBeTrue(string.Join("; ", indexed.Errors.Select(error => error.Message)));

            var result = await search.SearchAsync(SearchTestHelpers.Query(index, Occurance.MUST, new DateRangeQuery
            {
                Field = "Day",
                MinValue = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                MaxValue = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc)
            }), Token);

            // "2" is 2026-01-01T23:30Z, so only the date-only value is inside the range.
            result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
            result.Value.Items.Select(item => item.Document.Id).ShouldBe(["1"]);
        }
        finally
        {
            try
            {
                await indexing.DeleteIndexAsync(index, CancellationToken.None);
            }
            catch (Exception)
            {
                // Cleanup must not hide the test's own failure.
            }
        }
    }

    [Fact]
    public async Task SearchAsync_RangeOnSeveralValues_NeedsOneValueWithinBothBounds()
    {
        await WithIndexAsync(index =>
        [
            Document(index, "1", new() { { "Price", [10, 60] }, { "Day", ["2026-01-01", "2026-12-31"] } }),
            Document(index, "2", new() { { "Price", [10, 30] }, { "Day", ["2026-01-01", "2026-06-15T10:00:00Z"] } }),
        ], async index =>
        {
            (await IdsAsync(index, new NumericRange { Field = "Price", MinValue = 20, MaxValue = 50 })).ShouldBe(["2"]);
            (await IdsAsync(index, new DateRangeQuery
            {
                Field = "Day",
                MinValue = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                MaxValue = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
            })).ShouldBe(["2"]);
        });
    }

    [Fact]
    public async Task SearchAsync_TermOfSeveralWords_MatchesNothingAsATokenQueryDoes()
    {
        await WithIndexAsync(index => [Document(index, "1", new() { { "Name", ["Jane Doe"] } })], async index =>
        {
            (await IdsAsync(index, new TermSubQuery { Field = "Name", Value = "Jane Doe" })).ShouldBeEmpty();
            (await IdsAsync(index, new TermSubQuery { Field = "Name", Value = "JANE" })).ShouldBe(["1"]);
        });
    }

    [Fact]
    public async Task SearchAsync_PrefixPhrase_MatchesWordsInOrderWithTheLastAsPrefix()
    {
        await WithIndexAsync(index =>
        [
            Document(index, "1", new() { { "Name", ["John Doe"] } }),
            Document(index, "2", new() { { "Name", ["Doe John"] } }),
            Document(index, "3", new() { { "Name", ["John"] }, { "Other", ["Doe"] } }),
        ], async index =>
        {
            (await IdsAsync(index, new PrefixPhraseSubQuery { Field = "Name", Value = "john do" })).ShouldBe(["1"]);
            (await IdsAsync(index, new PrefixSubQuery { Field = "Name", Value = "jo" })).ShouldBe(["1", "2", "3"]);
            (await IdsAsync(index, new PrefixSubQuery { Field = "Name", Value = "jo" }, Occurance.MUSTNOT)).ShouldBeEmpty();
            // No single word starts with two words, so nothing matches and its negation matches everything.
            (await IdsAsync(index, new PrefixSubQuery { Field = "Name", Value = "john do" })).ShouldBeEmpty();
            (await IdsAsync(index, new PrefixSubQuery { Field = "Name", Value = "john do" }, Occurance.MUSTNOT)).ShouldBe(["1", "2", "3"]);
        });
    }

    [Fact]
    public async Task SearchAsync_Fuzzy_MatchesWithinTheEditDistance()
    {
        await WithIndexAsync(index =>
        [
            Document(index, "1", new() { { "Name", ["Jane Doe"] } }),
            Document(index, "2", new() { { "Name", ["Jade Doe"] } }),
            Document(index, "3", new() { { "Name", ["John Doe"] } }),
        ], async index =>
        {
            // "jane" has 4 letters, so AUTO allows one edit: "jade" is one away, "john" three.
            (await IdsAsync(index, new FuzzyQuery { Field = "Name", Value = "jane" })).ShouldBe(["1", "2"]);
            (await IdsAsync(index, new FuzzyQuery { Field = "Name", Value = "jane", Fuziness = Tuple.Create(0, 0) })).ShouldBe(["1"]);
            (await IdsAsync(index, new FuzzyQuery { Field = "Name", Value = "jane", PrefixLenght = 3 })).ShouldBe(["1"]);
        });
    }

    [Fact]
    public async Task SearchAsync_FuzzyWithoutTheExtension_FailsNamingIt()
    {
        var schema = $"nofuzzy_{Guid.NewGuid():N}";
        var index = $"pg-fuzzy-{Guid.NewGuid():N}";
        await WithIndexAsync(_ => [], async _ =>
        {
            // levenshtein resolves through the search path; a connection whose search path lacks the schema holding
            // the extension sees no such function, as a database without the extension does.
            var builder = new NpgsqlConnectionStringBuilder(postgres.EnsureStarted()) { SearchPath = schema };
            await using var restricted = NpgsqlDataSource.Create(builder.ConnectionString);
            await using (var create = restricted.CreateCommand(
                             $"CREATE SCHEMA \"{schema}\"; CREATE TABLE \"{schema}\".\"{index}\" (id text PRIMARY KEY, type text NOT NULL DEFAULT '', fields jsonb NOT NULL, " +
                             "search tsvector GENERATED ALWAYS AS (pg_catalog.jsonb_to_tsvector('simple'::regconfig, fields, '[\"string\"]')) STORED)"))
            {
                await create.ExecuteNonQueryAsync(Token);
            }

            try
            {
                var options = new TestOptionsMonitor<Configuration.PostgreSqlOptions>(new Configuration.PostgreSqlOptions
                {
                    Schema = schema,
                    SharedIndexSettings = new Configuration.PostgreSqlIndexSettings { ConnectionString = builder.ConnectionString },
                });
                using var factory = new PostgreSqlDataSourceFactory(new PostgreSqlClientSettingsManager(options), new EmptyServiceProvider());
                var provider = new PostgreSqlSearchProviderAsync(
                    new PostgreSqlQueryTranslateService([new Services.Queries.FuzzySubQueryTranslator()]), factory, options);

                var result = await provider.SearchAsync(SearchTestHelpers.Query(index, Occurance.MUST, new FuzzyQuery { Field = "Name", Value = "jane" }), Token);

                result.Errors.ShouldHaveSingleItem().Message.ShouldBe(PostgreSqlCommands.MissingFuzzyExtension);
            }
            finally
            {
                await using var drop = restricted.CreateCommand($"DROP SCHEMA \"{schema}\" CASCADE");
                await drop.ExecuteNonQueryAsync(CancellationToken.None);
            }
        });
    }

    [Fact]
    public async Task SearchAsync_Spatial_MatchesPointsWithinTheDistance()
    {
        await WithIndexAsync(index =>
        [
            // Warsaw, Kraków (about 252 km from Warsaw) and Berlin (about 517 km), in each point format.
            Document(index, "1", new() { { "Location", ["52.2297,21.0122"] } }),
            Document(index, "2", new() { { "Location", [new Dictionary<string, object> { ["lat"] = 50.0647, ["lon"] = 19.9450 }] } }),
            Document(index, "3", new() { { "Location", [new Dictionary<string, object> { ["lat"] = 52.52, ["lng"] = 13.405 }] } }),
            Document(index, "4", new() { { "Location", ["not a point"] } }),
        ], async index =>
        {
            (await IdsAsync(index, new SpatialSearchQuery { Field = "Location", Lat = 52.2297, Lng = 21.0122, Distance = 300 })).ShouldBe(["1", "2"]);
            (await IdsAsync(index, new SpatialSearchQuery { Field = "Location", Lat = 52.2297, Lng = 21.0122, Distance = 200, UnitOfDistance = "mi" }))
                .ShouldBe(["1", "2"]);
            (await IdsAsync(index, new SpatialSearchQuery { Field = "Location", Lat = 52.2297, Lng = 21.0122, Distance = 10 }, Occurance.MUSTNOT))
                .ShouldBe(["2", "3", "4"]);
        });
    }

    [Fact]
    public async Task SearchAsync_StringRange_ComparesWholeValuesByBytes()
    {
        await WithIndexAsync(index =>
        [
            Document(index, "1", new() { { "Code", ["B"] } }),
            Document(index, "2", new() { { "Code", ["a"] } }),
            Document(index, "3", new() { { "Code", ["c"] } }),
        ], async index =>
        {
            (await IdsAsync(index, new StringRange { Field = "Code", MinValue = "A", MaxValue = "b" })).ShouldBe(["1", "2"]);
            (await IdsAsync(index, new StringRange { Field = "Code", MinValue = "a", IncludeMinEdge = false })).ShouldBe(["3"]);
        });
    }

    [Fact]
    public async Task SearchAsync_Paging_ReturnsEachDocumentOnceWithTheTotal()
    {
        await WithIndexAsync(index => Enumerable.Range(1, 5).Select(i => Document(index, $"d{i}", new() { { "Rank", [i % 2] } })), async index =>
        {
            var seen = new List<string>();
            for (var page = 1; page <= 3; page++)
            {
                var result = await searchService.SearchAsync(new SearchQuery { Index = index, Page = page, PageSize = 2 }, Token);
                result.IsSuccess.ShouldBeTrue();
                result.Value.Pagination.TotalCount.ShouldBe(5);
                result.Value.Pagination.TotalPages.ShouldBe(3);
                seen.AddRange(result.Value.Items.Select(item => item.Document.Id));
            }

            seen.ShouldBe(["d1", "d2", "d3", "d4", "d5"]);
        });
    }

    [Fact]
    public async Task SearchAsync_Properties_ReturnsOnlyThoseFields()
    {
        await WithIndexAsync(index => [Document(index, "1", new() { { "Name", ["Jane"] }, { "Age", [25] } })], async index =>
        {
            var result = await searchService.SearchAsync(new SearchQuery { Index = index, Properties = ["Name"] }, Token);

            result.IsSuccess.ShouldBeTrue();
            result.Value.Items.ShouldHaveSingleItem().Document.CustomProperties.Keys.ShouldBe(["Name"]);
        });
    }

    [Fact]
    public async Task SearchAsync_LuceneSubQuery_FailsNamingIt()
    {
        var result = await searchService.SearchAsync(
            SearchTestHelpers.Query("people", Occurance.MUST, new LuceneSubQuery { LuceneQuery = "Name:jane" }), Token);

        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("does not support LuceneSubQuery");
    }

    [Fact]
    public async Task CreateIndexAsync_ExistingIndex_ReturnsFalse()
    {
        await WithIndexAsync(_ => [], async _ =>
        {
            var index = $"pg-create-{Guid.NewGuid():N}";
            try
            {
                (await indexingProvider.CreateIndexAsync(index, Token)).Value.ShouldBeTrue();
                (await indexingProvider.CreateIndexAsync(index, Token)).Value.ShouldBeFalse();
            }
            finally
            {
                await indexingProvider.DeleteIndexAsync(index, CancellationToken.None);
            }
        });
    }

    [Fact]
    public async Task CreateIndexAsync_NameTakenByACompositeType_FailsAndDoesNotExist()
    {
        var index = $"pg-type-{Guid.NewGuid():N}";
        await using var dataSource = NpgsqlDataSource.Create(postgres.EnsureStarted());
        await using (var create = dataSource.CreateCommand($"CREATE TYPE public.\"{index}\" AS (a int)"))
        {
            await create.ExecuteNonQueryAsync(Token);
        }

        try
        {
            (await indexingProvider.IndexExistsAsync(index, Token)).Value.ShouldBeFalse();
            (await indexingProvider.CreateIndexAsync(index, Token)).Errors.ShouldHaveSingleItem().Message.ShouldContain("not a table");
            (await indexingProvider.EnsureIndexExistsAsync(index, Token)).Errors.ShouldHaveSingleItem().Message.ShouldContain("not a table");
        }
        finally
        {
            await using var drop = dataSource.CreateCommand($"DROP TYPE IF EXISTS public.\"{index}\"");
            await drop.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task DeleteIndexAsync_MissingIndex_ReturnsFalse()
    {
        (await indexingProvider.DeleteIndexAsync($"pg-missing-{Guid.NewGuid():N}", Token)).Value.ShouldBeFalse();
    }

    private sealed record Dimensions(int Width, string Unit);

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
