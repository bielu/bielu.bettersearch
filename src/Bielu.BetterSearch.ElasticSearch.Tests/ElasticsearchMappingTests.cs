using System.Text.Json;
using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using EsQuery = Elastic.Clients.Elasticsearch.QueryDsl.Query;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

/// <summary>Configured field mappings, checked against a real Elasticsearch.</summary>
public sealed class ElasticsearchMappingTests(
    ElasticsearchResource elasticsearch,
    IClientFactoryAsync<ElasticsearchClient> clientFactory,
    IQueryTranslateServiceAsync<EsQuery> queryTranslateService,
    IPostFilterTranslateServiceAsync<EsQuery> postFilterTranslateService)
{
    private sealed class SortField : ISortOrderField
    {
        public string FieldName { get; set; } = string.Empty;
        public bool Desc { get; set; }
        public int Order { get; set; }
    }

    private sealed record Providers(string Index, ElasticSearchIndexingProviderAsync Indexing, ElasticSearchSearchProviderAsync Search);

    /// <summary>Providers for a new index of its own, whose <c>Indexes</c> entry has <paramref name="mappings"/>.</summary>
    private Providers For(params ElasticSearchFieldMapping[] mappings) => ForIndex($"mapping-{Guid.NewGuid():N}", mappings);

    private Providers ForIndex(string index, params ElasticSearchFieldMapping[] mappings)
    {
        var monitor = new FixedOptionsMonitor(new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings
            {
                ConnectionString = elasticsearch.Endpoint!.ToString(),
                AuthenticationType = AuthenticationType.NONE,
            },
            Indexes = new Dictionary<string, ElasticSearchIndexSettings> { [index] = new() { Mappings = [.. mappings] } },
        });
        var settingsManager = new ElasticSearchClientSettingsManager(monitor);
        return new Providers(
            index,
            new ElasticSearchIndexingProviderAsync(clientFactory, NullLogger<ElasticSearchIndexingProviderAsync>.Instance, settingsManager),
            new ElasticSearchSearchProviderAsync(queryTranslateService, postFilterTranslateService, clientFactory,
                new ElasticSearchResultMapper(), monitor, settingsManager));
    }

    private static ElasticSearchFieldMapping Map(string field, ElasticSearchFieldType type) => new() { Field = field, Type = type };

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

        var refreshed = await (await clientFactory.GetOrCreateClientAsync(providers.Index)).Indices.RefreshAsync(providers.Index, TestContext.Current.CancellationToken);
        refreshed.IsValidResponse.ShouldBeTrue();
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
        var response = await client.Transport.RequestAsync<StringResponse>(Elastic.Transport.HttpMethod.GET, $"/{index}/_mapping", cancellationToken: TestContext.Current.CancellationToken);
        response.ApiCallDetails.HttpStatusCode.ShouldBe(200);
        return JsonDocument.Parse(response.Body).RootElement
            .GetProperty(index).GetProperty("mappings").GetProperty("properties").GetProperty("fields").GetProperty("properties").Clone();
    }

    [Fact]
    public async Task TermQuery_KeywordField_MatchesTheWholeValueUnderItsPlainName()
    {
        var providers = For(Map("City", ElasticSearchFieldType.Keyword));
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
        var providers = For(Map("City", ElasticSearchFieldType.Keyword));
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
        var providers = For(Map("Name", ElasticSearchFieldType.Text));
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
    public async Task SearchAsync_SortOnMultiValuedTextField_AscendingUsesTheLowestValue()
    {
        var providers = For(Map("Name", ElasticSearchFieldType.Text));
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
        var providers = For(Map("Name", ElasticSearchFieldType.Text));
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
        var providers = For(Map("Name", ElasticSearchFieldType.Text));
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
        var providers = For(Map("City", ElasticSearchFieldType.Keyword));
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
        var providers = For(Map("Location", ElasticSearchFieldType.GeoPoint));
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
            new ElasticSearchFieldMapping { Field = "Title", Type = ElasticSearchFieldType.Text, Analyzer = "english" },
            new ElasticSearchFieldMapping { Field = "Born", Type = ElasticSearchFieldType.Date, Format = "yyyy-MM-dd" },
            Map("Views", ElasticSearchFieldType.Long),
            Map("Active", ElasticSearchFieldType.Boolean));
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
        var providers = For(new ElasticSearchFieldMapping { Field = "Title", Type = ElasticSearchFieldType.Text, Analyzer = "no_such_analyzer" });
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
        var providers = For(Map("City", ElasticSearchFieldType.Keyword));
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
        var providers = For(Map("City", ElasticSearchFieldType.Keyword));
        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(providers.Index);
            (await client.Indices.CreateAsync(providers.Index, TestContext.Current.CancellationToken)).IsValidResponse.ShouldBeTrue();

            var ensured = await providers.Indexing.EnsureIndexExistsAsync(providers.Index, TestContext.Current.CancellationToken);

            ensured.IsSuccess.ShouldBeTrue(string.Join("; ", ensured.Errors.Select(e => e.Message)));
            ensured.Value.ShouldBeFalse();
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
        var providers = ForIndex(unmapped.Index, Map("City", ElasticSearchFieldType.Keyword));
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
