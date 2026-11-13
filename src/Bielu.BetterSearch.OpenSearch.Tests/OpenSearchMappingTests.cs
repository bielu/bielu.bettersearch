using System.Text.Json;
using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenSearch.Client;
using OpenSearch.Net;
using Shouldly;

namespace Bielu.BetterSearch.OpenSearch.Tests;

/// <summary>Configured field mappings, checked against a real OpenSearch.</summary>
public sealed class OpenSearchMappingTests(
    OpenSearchResource openSearch,
    IClientFactoryAsync<IOpenSearchClient> clientFactory,
    IQueryTranslateServiceAsync<QueryContainer> queryTranslateService,
    IPostFilterTranslateServiceAsync<QueryContainer> postFilterTranslateService)
{
    private sealed class SortField : ISortOrderField
    {
        public string FieldName { get; set; } = string.Empty;
        public bool Desc { get; set; }
        public int Order { get; set; }
    }

    private sealed class FixedOptionsMonitor(OpenSearchOptions value) : IOptionsMonitor<OpenSearchOptions>
    {
        public OpenSearchOptions CurrentValue => value;
        public OpenSearchOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<OpenSearchOptions, string?> listener) => null;
    }

    private sealed record Providers(string Index, OpenSearchIndexingProviderAsync Indexing, OpenSearchSearchProviderAsync Search);

    /// <summary>Providers for a new index of its own, whose <c>Indexes</c> entry has <paramref name="mappings"/>.</summary>
    private Providers For(params OpenSearchFieldMapping[] mappings) => ForIndex($"mapping-{Guid.NewGuid():N}", mappings);

    private Providers ForIndex(string index, params OpenSearchFieldMapping[] mappings)
    {
        var monitor = new FixedOptionsMonitor(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings
            {
                ConnectionString = openSearch.Endpoint!.ToString(),
                AuthenticationType = OpenSearchAuthenticationType.NONE,
            },
            Indexes = new Dictionary<string, OpenSearchIndexSettings> { [index] = new() { Mappings = [.. mappings] } },
        });
        var settingsManager = new OpenSearchClientSettingsManager(monitor);
        return new Providers(
            index,
            new OpenSearchIndexingProviderAsync(clientFactory, NullLogger<OpenSearchIndexingProviderAsync>.Instance, settingsManager),
            new OpenSearchSearchProviderAsync(queryTranslateService, postFilterTranslateService, clientFactory,
                new OpenSearchResultMapper(), monitor, settingsManager));
    }

    private static OpenSearchFieldMapping Map(string field, OpenSearchFieldType type) => new() { Field = field, Type = type };

    private static SearchDocument Document(string index, string id, string field, params object[] values) => new()
    {
        Id = id,
        Type = "Place",
        Index = index,
        Fields = new Dictionary<string, IList<object>> { { field, values.ToList() } }
    };

    private async Task IndexAsync(Providers providers, params SearchDocument[] documents)
    {
        var created = await providers.Indexing.EnsureIndexExistsAsync(providers.Index, TestContext.Current.CancellationToken);
        created.IsSuccess.ShouldBeTrue(string.Join("; ", created.Errors.Select(e => e.Message)));
        foreach (var document in documents)
        {
            var indexed = await providers.Indexing.IndexDocumentAsync(document, TestContext.Current.CancellationToken);
            indexed.IsSuccess.ShouldBeTrue(string.Join("; ", indexed.Errors.Select(e => e.Message)));
        }

        var refreshed = await (await clientFactory.GetOrCreateClientAsync(providers.Index)).Indices.RefreshAsync(providers.Index, ct: TestContext.Current.CancellationToken);
        refreshed.IsValid.ShouldBeTrue();
    }

    private static async Task<List<string>> SearchIdsAsync(Providers providers, SearchQuery query, bool sorted = true)
    {
        query.Index = providers.Index;
        query.PageSize = 100;
        var result = await providers.Search.SearchAsync(query, TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
        var ids = result.Value.Items.Select(item => item.Document.Id);
        return (sorted ? ids.OrderBy(id => id, StringComparer.Ordinal) : ids).ToList();
    }

    private static SearchQuery Term(string field, object value)
    {
        var query = new SearchQuery();
        query.Add(Occurance.MUST, new TermSubQuery { Field = field, Value = value });
        return query;
    }

    private static SearchQuery SortedBy(string field) => new() { SortOrder = [new SortField { FieldName = field }] };

    private async Task DeleteAsync(Providers providers)
    {
        try
        {
            await providers.Indexing.DeleteIndexAsync(providers.Index, CancellationToken.None);
        }
        catch (Exception)
        {
            // Cleanup must not hide the test's own failure.
        }
    }

    private async Task<JsonElement> MappedFieldsAsync(string index)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index);
        var response = await client.LowLevel.Indices.GetMappingAsync<StringResponse>(index, ctx: TestContext.Current.CancellationToken);
        response.HttpStatusCode.ShouldBe(200);
        return JsonDocument.Parse(response.Body).RootElement
            .GetProperty(index).GetProperty("mappings").GetProperty("properties").GetProperty("fields").GetProperty("properties").Clone();
    }

    [Fact]
    public async Task TermQuery_KeywordField_MatchesTheWholeValueUnderItsPlainName()
    {
        var providers = For(Map("City", OpenSearchFieldType.Keyword));
        try
        {
            await IndexAsync(providers,
                Document(providers.Index, "1", "City", "New York"),
                Document(providers.Index, "2", "City", "York"));

            // A text field would match "York" in both; the keyword field holds the whole value, matched ignoring case.
            (await SearchIdsAsync(providers, Term("City", "York"))).ShouldBe(["2"]);
            (await SearchIdsAsync(providers, Term("City", "new york"))).ShouldBe(["1"]);
        }
        finally
        {
            await DeleteAsync(providers);
        }
    }

    [Fact]
    public async Task SearchAsync_SortOnKeywordField_SortsUnderItsPlainName()
    {
        var providers = For(Map("City", OpenSearchFieldType.Keyword));
        try
        {
            await IndexAsync(providers,
                Document(providers.Index, "1", "City", "Warsaw"),
                Document(providers.Index, "2", "City", "Gdansk"),
                Document(providers.Index, "3", "City", "Krakow"));

            (await SearchIdsAsync(providers, SortedBy("City"), sorted: false)).ShouldBe(["2", "3", "1"]);
        }
        finally
        {
            await DeleteAsync(providers);
        }
    }

    [Fact]
    public async Task SearchAsync_SortOnTextField_SortsOnTheKeywordSubFieldCaseSensitively()
    {
        var providers = For(Map("Name", OpenSearchFieldType.Text));
        try
        {
            await IndexAsync(providers,
                Document(providers.Index, "1", "Name", "apple"),
                Document(providers.Index, "2", "Name", "Banana"),
                Document(providers.Index, "3", "Name", "cherry"));

            // The keyword sub-field holds the value as given, so upper case sorts before lower case.
            (await SearchIdsAsync(providers, SortedBy("Name"), sorted: false)).ShouldBe(["2", "1", "3"]);
        }
        finally
        {
            await DeleteAsync(providers);
        }
    }

    [Fact]
    public async Task SearchAsync_SortOnTextFieldNamedFields_SortsOnItsKeywordSubField()
    {
        var providers = For(Map("fields", OpenSearchFieldType.Text));
        try
        {
            await IndexAsync(providers,
                Document(providers.Index, "1", "fields", "cherry"),
                Document(providers.Index, "2", "fields", "apple"),
                Document(providers.Index, "3", "fields", "banana"));

            // The field is stored at fields.fields, so its keyword sub-field is fields.fields.keyword, not fields.keyword.
            (await SearchIdsAsync(providers, SortedBy("fields"), sorted: false)).ShouldBe(["2", "3", "1"]);
            (await SearchIdsAsync(providers, SortedBy("fields.fields"), sorted: false)).ShouldBe(["2", "3", "1"]);
        }
        finally
        {
            await DeleteAsync(providers);
        }
    }

    [Fact]
    public async Task SearchAsync_SortOnScoreWithTextFieldNamedScore_SortsByRelevance()
    {
        var providers = For(Map("_score", OpenSearchFieldType.Text));
        try
        {
            SearchDocument ScoredDocument(string id, string name, string score) => new()
            {
                Id = id,
                Type = "Place",
                Index = providers.Index,
                Fields = new Dictionary<string, IList<object>> { { "Name", [name] }, { "_score", [score] } }
            };

            // The shorter name scores higher; the document field orders them the other way round.
            await IndexAsync(providers,
                ScoredDocument("1", "apple", "aaa"),
                ScoredDocument("2", "apple banana cherry date", "zzz"));

            var byRelevance = Term("Name", "apple");
            byRelevance.SortOrder = [new SortField { FieldName = "_score", Desc = true }];
            (await SearchIdsAsync(providers, byRelevance, sorted: false)).ShouldBe(["1", "2"]);

            // The document field is still sortable through its prefixed name.
            var byField = Term("Name", "apple");
            byField.SortOrder = [new SortField { FieldName = "fields._score", Desc = true }];
            (await SearchIdsAsync(providers, byField, sorted: false)).ShouldBe(["2", "1"]);
        }
        finally
        {
            await DeleteAsync(providers);
        }
    }

    [Fact]
    public async Task SearchAsync_SortOnMultiValuedTextField_AscendingUsesTheLowestValue()
    {
        var providers = For(Map("Name", OpenSearchFieldType.Text));
        try
        {
            await IndexAsync(providers,
                Document(providers.Index, "1", "Name", "beta"),
                Document(providers.Index, "2", "Name", "zeta", "alpha"));

            (await SearchIdsAsync(providers, SortedBy("Name"), sorted: false)).ShouldBe(["2", "1"]);
        }
        finally
        {
            await DeleteAsync(providers);
        }
    }

    [Fact]
    public async Task SearchAsync_SortOnTextValueLongerThanIgnoreAbove_SortsItAsMissing()
    {
        var providers = For(Map("Name", OpenSearchFieldType.Text));
        try
        {
            await IndexAsync(providers,
                Document(providers.Index, "1", "Name", new string('a', 257)),
                Document(providers.Index, "2", "Name", "b"));

            // "aaa..." sorts before "b", but at 257 characters it is not in the keyword sub-field, so it comes last.
            (await SearchIdsAsync(providers, SortedBy("Name"), sorted: false)).ShouldBe(["2", "1"]);
        }
        finally
        {
            await DeleteAsync(providers);
        }
    }

    [Fact]
    public async Task TermQuery_TextField_StillMatchesOneWord()
    {
        var providers = For(Map("Name", OpenSearchFieldType.Text));
        try
        {
            await IndexAsync(providers,
                Document(providers.Index, "1", "Name", "John Doe"),
                Document(providers.Index, "2", "Name", "Jane Smith"));

            (await SearchIdsAsync(providers, Term("Name", "doe"))).ShouldBe(["1"]);
        }
        finally
        {
            await DeleteAsync(providers);
        }
    }

    [Fact]
    public async Task TermQuery_UnmappedField_KeepsDynamicMapping()
    {
        var providers = For(Map("City", OpenSearchFieldType.Keyword));
        try
        {
            await IndexAsync(providers,
                Document(providers.Index, "1", "Name", "John Doe"),
                Document(providers.Index, "2", "Name", "Jane Doe"),
                Document(providers.Index, "3", "Name", "Jane Smith"));

            (await SearchIdsAsync(providers, Term("Name", "doe"))).ShouldBe(["1", "2"]);
            (await MappedFieldsAsync(providers.Index)).GetProperty("Name").GetProperty("type").GetString().ShouldBe("text");
        }
        finally
        {
            await DeleteAsync(providers);
        }
    }

    [Fact]
    public async Task SpatialSearchQuery_GeoPointField_ReturnsThePointsWithinTheDistance()
    {
        var providers = For(Map("Location", OpenSearchFieldType.GeoPoint));
        try
        {
            await IndexAsync(providers,
                Document(providers.Index, "warsaw", "Location", "52.2297,21.0122"),
                Document(providers.Index, "krakow", "Location", new Dictionary<string, object> { ["lat"] = 50.0647, ["lon"] = 19.9450 }),
                Document(providers.Index, "piaseczno", "Location", "52.0811,21.0238"));
            var query = new SearchQuery();
            query.Add(Occurance.MUST, new SpatialSearchQuery { Field = "Location", Lat = 52.2297, Lng = 21.0122, Distance = 20 });

            // Piaseczno is about 17 km from Warsaw, Krakow about 250 km.
            (await SearchIdsAsync(providers, query)).ShouldBe(["piaseczno", "warsaw"]);
        }
        finally
        {
            await DeleteAsync(providers);
        }
    }

    [Fact]
    public async Task CreateIndexAsync_Mappings_AppliesTypeAnalyzerFormatAndKeywordSubField()
    {
        var providers = For(
            new OpenSearchFieldMapping { Field = "Title", Type = OpenSearchFieldType.Text, Analyzer = "english" },
            new OpenSearchFieldMapping { Field = "Born", Type = OpenSearchFieldType.Date, Format = "yyyy-MM-dd" },
            Map("Views", OpenSearchFieldType.Long),
            Map("Active", OpenSearchFieldType.Boolean));
        try
        {
            var created = await providers.Indexing.CreateIndexAsync(providers.Index, TestContext.Current.CancellationToken);
            created.IsSuccess.ShouldBeTrue(string.Join("; ", created.Errors.Select(e => e.Message)));
            created.Value.ShouldBeTrue();

            var fields = await MappedFieldsAsync(providers.Index);
            var title = fields.GetProperty("Title");
            title.GetProperty("type").GetString().ShouldBe("text");
            title.GetProperty("analyzer").GetString().ShouldBe("english");
            title.GetProperty("fields").GetProperty("keyword").GetProperty("type").GetString().ShouldBe("keyword");
            title.GetProperty("fields").GetProperty("keyword").GetProperty("ignore_above").GetInt32().ShouldBe(256);
            fields.GetProperty("Born").GetProperty("format").GetString().ShouldBe("yyyy-MM-dd");
            fields.GetProperty("Views").GetProperty("type").GetString().ShouldBe("long");
            fields.GetProperty("Active").GetProperty("type").GetString().ShouldBe("boolean");
        }
        finally
        {
            await DeleteAsync(providers);
        }
    }

    [Fact]
    public async Task CreateIndexAsync_UnknownAnalyzer_ReturnsFailureNamingIt()
    {
        var providers = For(new OpenSearchFieldMapping { Field = "Title", Type = OpenSearchFieldType.Text, Analyzer = "no_such_analyzer" });
        try
        {
            var created = await providers.Indexing.CreateIndexAsync(providers.Index, TestContext.Current.CancellationToken);

            created.IsFailed.ShouldBeTrue();
            created.Errors.Single().Message.ShouldContain("no_such_analyzer");
        }
        finally
        {
            await DeleteAsync(providers);
        }
    }

    [Fact]
    public async Task CreateIndexAsync_IndexExists_ReturnsFalse()
    {
        var providers = For(Map("City", OpenSearchFieldType.Keyword));
        try
        {
            (await providers.Indexing.CreateIndexAsync(providers.Index, TestContext.Current.CancellationToken)).Value.ShouldBeTrue();

            var second = await providers.Indexing.CreateIndexAsync(providers.Index, TestContext.Current.CancellationToken);

            second.IsSuccess.ShouldBeTrue();
            second.Value.ShouldBeFalse();
        }
        finally
        {
            await DeleteAsync(providers);
        }
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_ExistingIndex_AddsTheMappings()
    {
        var providers = For(Map("City", OpenSearchFieldType.Keyword));
        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(providers.Index);
            (await client.Indices.CreateAsync(providers.Index, ct: TestContext.Current.CancellationToken)).IsValid.ShouldBeTrue();

            var ensured = await providers.Indexing.EnsureIndexExistsAsync(providers.Index, TestContext.Current.CancellationToken);

            ensured.IsSuccess.ShouldBeTrue(string.Join("; ", ensured.Errors.Select(e => e.Message)));
            // OpenSearch answers true for an index that exists, as it did before mappings.
            ensured.Value.ShouldBeTrue();
            (await MappedFieldsAsync(providers.Index)).GetProperty("City").GetProperty("type").GetString().ShouldBe("keyword");
        }
        finally
        {
            await DeleteAsync(providers);
        }
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_MappingConflictsWithTheExistingField_ReturnsFailureNamingTheField()
    {
        var unmapped = For();
        var providers = ForIndex(unmapped.Index, Map("City", OpenSearchFieldType.Keyword));
        try
        {
            // Dynamic mapping makes City a text field first.
            await IndexAsync(unmapped, Document(unmapped.Index, "1", "City", "Warsaw"));

            var ensured = await providers.Indexing.EnsureIndexExistsAsync(unmapped.Index, TestContext.Current.CancellationToken);

            ensured.IsFailed.ShouldBeTrue();
            ensured.Errors.Single().Message.ShouldContain("adding the configured mappings");
            ensured.Errors.Single().Message.ShouldContain("fields.City");
        }
        finally
        {
            await DeleteAsync(unmapped);
        }
    }
}
