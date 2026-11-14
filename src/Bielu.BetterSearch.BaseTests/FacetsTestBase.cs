using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

/// <summary>
/// <c>FacetQueries</c> through <see cref="ISearchServiceAsync"/>, inherited by every provider's tests. Every value
/// has its own count, so a facet that counts the wrong documents, or splits a value into words, gives other numbers.
/// The facet fields are <see cref="Color"/> and <see cref="Brand"/>, which providers with field mappings map as text,
/// and the numeric <see cref="Stock"/>.
/// </summary>
public abstract class FacetsTestBase(ISearchServiceAsync searchService, IIndexingProviderAsync indexingProvider)
{
    public const string Color = "FacetColor";
    public const string Brand = "FacetBrand";
    public const string Stock = "FacetStock";

    protected static CancellationToken Token => TestContext.Current.CancellationToken;

    protected ISearchServiceAsync SearchService { get; } = searchService;

    protected IIndexingProviderAsync IndexingProvider { get; } = indexingProvider;

    /// <summary>The message of a provider that has no facets at all, or <c>null</c> when it has them.</summary>
    protected virtual string? UnsupportedFacets => null;

    /// <summary>
    /// Text that the failure of a provider which counts only top-level term facets contains, for a multi-term,
    /// filter or nested facet; <c>null</c> when the provider has them all.
    /// </summary>
    protected virtual string? UnsupportedComplexFacets => null;

    /// <summary>Makes the documents indexed so far searchable; nothing to do where indexing already is.</summary>
    protected virtual Task RefreshAsync(string index) => Task.CompletedTask;

    private static SearchDocument Product(string index, string id, string color, string brand, long? stock = null)
    {
        var fields = new Dictionary<string, IList<object>> { { Color, [color] }, { Brand, [brand] } };
        if (stock != null)
        {
            fields[Stock] = [stock.Value];
        }

        return new SearchDocument { Id = id, Type = "Product", Index = index, Fields = fields };
    }

    /// <summary>
    /// Red 3, Dark Blue 2, Green 1; acme 3, globex 2, initech 1; stock 5 twice and 7 once. Acme has two red and one
    /// dark blue product, globex one of each, initech the green one.
    /// </summary>
    private async Task<string> CreateProductsAsync()
    {
        var index = $"facets-{Guid.NewGuid():N}";
        var indexed = await IndexingProvider.IndexMultipleDocumentsAsync(
        [
            Product(index, "1", "Red", "acme", 5),
            Product(index, "2", "Red", "acme", 5),
            Product(index, "3", "Red", "globex", 7),
            Product(index, "4", "Dark Blue", "acme"),
            Product(index, "5", "Dark Blue", "globex"),
            Product(index, "6", "Green", "initech")
        ], Token);
        indexed.IsSuccess.ShouldBeTrue(string.Join("; ", indexed.Errors.Select(error => error.Message)));
        await RefreshAsync(index);
        return index;
    }

    private async Task RunAsync(Func<string, Task> test)
    {
        var index = await CreateProductsAsync();
        try
        {
            await test(index);
        }
        finally
        {
            try
            {
                await IndexingProvider.DeleteIndexAsync(index, CancellationToken.None);
            }
            catch (Exception)
            {
                // Cleanup must not hide the test's own failure.
            }
        }
    }

    private Task<Result<ISearchResult>> SearchAsync(SearchQuery query)
    {
        query.PageSize = 100;
        return SearchService.SearchAsync(query, Token);
    }

    private static TermSubQuery BrandIs(string brand) => new() { Field = Brand, Value = brand };

    private static List<(string Key, long? Count)> Counts(BucketAggregation? aggregation)
    {
        aggregation.ShouldNotBeNull();
        return aggregation.Buckets.Cast<KeyBucket>().Select(bucket => (bucket.Key, bucket.TotalOfDocuments)).ToList();
    }

    private static Dictionary<string, long?> CountsByKey(BucketAggregation? aggregation) =>
        Counts(aggregation).ToDictionary(bucket => bucket.Key, bucket => bucket.Count);

    /// <summary>
    /// Whether the provider ran the query: checks that it succeeded when <paramref name="unsupported"/> is <c>null</c>,
    /// and that it failed with a message containing <paramref name="unsupported"/> otherwise.
    /// </summary>
    private static bool Ran(Result<ISearchResult> result, string? unsupported)
    {
        if (unsupported == null)
        {
            result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
            return true;
        }

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain(unsupported);
        return false;
    }

    private bool RanTerms(Result<ISearchResult> result) => Ran(result, UnsupportedFacets);

    private bool RanComplex(Result<ISearchResult> result) => Ran(result, UnsupportedFacets ?? UnsupportedComplexFacets);

    [Fact]
    public async Task SearchAsync_TermFacet_ReturnsEveryValueWithItsCountMostFirst() => await RunAsync(async index =>
    {
        var query = new SearchQuery { Index = index, FacetQueries = [new TermAggregation { AggregationName = "colors", TermFieldName = Color }] };

        var result = await SearchAsync(query);

        if (RanTerms(result))
        {
            result.Value.Aggregations.Count.ShouldBe(1);
            Counts(result.Value.RetrieveBucketAggregation("colors")).ShouldBe([("Red", 3), ("Dark Blue", 2), ("Green", 1)]);
            result.Value.Items.Count().ShouldBe(6);
        }
    });

    [Fact]
    public async Task SearchAsync_NumericTermFacet_ReturnsValuesAsInvariantText() => await RunAsync(async index =>
    {
        var query = new SearchQuery { Index = index, FacetQueries = [new TermAggregation { AggregationName = "stock", TermFieldName = Stock }] };

        var result = await SearchAsync(query);

        if (RanTerms(result))
        {
            Counts(result.Value.RetrieveBucketAggregation("stock")).ShouldBe([("5", 2), ("7", 1)]);
        }
    });

    [Fact]
    public async Task SearchAsync_TermFacetWithSize_ReturnsOnlyTheMostFrequentValues() => await RunAsync(async index =>
    {
        var query = new SearchQuery { Index = index, FacetQueries = [new TermAggregation { AggregationName = "colors", TermFieldName = Color, Size = 2 }] };

        var result = await SearchAsync(query);

        if (RanTerms(result))
        {
            Counts(result.Value.RetrieveBucketAggregation("colors")).ShouldBe([("Red", 3), ("Dark Blue", 2)]);
        }
    });

    /// <summary>The post filter keeps acme's products as hits, but the counts are those of every product.</summary>
    [Fact]
    public async Task SearchAsync_TermFacetWithPostFilter_NarrowsHitsButNotCounts() => await RunAsync(async index =>
    {
        var query = new SearchQuery { Index = index, FacetQueries = [new TermAggregation { AggregationName = "colors", TermFieldName = Color }] };
        var acme = new BoolSearchSubQuery();
        acme.Add(Occurance.MUST, BrandIs("acme"));
        query.PostFilterQuery[Occurance.MUST] = [acme];

        var result = await SearchAsync(query);

        if (RanTerms(result))
        {
            result.Value.Items.Select(item => item.Document.Id).Order(StringComparer.Ordinal).ShouldBe(["1", "2", "4"]);
            Counts(result.Value.RetrieveBucketAggregation("colors")).ShouldBe([("Red", 3), ("Dark Blue", 2), ("Green", 1)]);
        }
    });

    /// <summary>The same filter in the main query narrows the counts too.</summary>
    [Fact]
    public async Task SearchAsync_TermFacetWithMainQuery_CountsOnlyMatches() => await RunAsync(async index =>
    {
        var query = SearchTestHelpers.Query(index, Occurance.MUST, BrandIs("acme"));
        query.FacetQueries = [new TermAggregation { AggregationName = "colors", TermFieldName = Color }];

        var result = await SearchAsync(query);

        if (RanTerms(result))
        {
            Counts(result.Value.RetrieveBucketAggregation("colors")).ShouldBe([("Red", 2), ("Dark Blue", 1)]);
        }
    });

    [Fact]
    public async Task SearchAsync_TwoTermFacets_ReturnsBothInQueryOrder() => await RunAsync(async index =>
    {
        var query = new SearchQuery
        {
            Index = index,
            FacetQueries =
            [
                new TermAggregation { AggregationName = "brands", TermFieldName = Brand },
                new TermAggregation { AggregationName = "colors", TermFieldName = Color }
            ]
        };

        var result = await SearchAsync(query);

        if (RanTerms(result))
        {
            result.Value.Aggregations.Select(aggregation => aggregation.AggregationName).ShouldBe(["brands", "colors"]);
            Counts(result.Value.RetrieveBucketAggregation("brands")).ShouldBe([("acme", 3), ("globex", 2), ("initech", 1)]);
            Counts(result.Value.RetrieveBucketAggregation("colors")).ShouldBe([("Red", 3), ("Dark Blue", 2), ("Green", 1)]);
        }
    });

    [Fact]
    public async Task SearchAsync_MultiTermFacet_ReturnsEveryCombinationWithItsCount() => await RunAsync(async index =>
    {
        var query = new SearchQuery
        {
            Index = index,
            FacetQueries =
            [
                new MultiTermAggreation
                {
                    AggregationName = "colorBrands",
                    Terms = [new SimpleTerm { TermFieldName = Color }, new SimpleTerm { TermFieldName = Brand }]
                }
            ]
        };

        var result = await SearchAsync(query);

        if (RanComplex(result))
        {
            var aggregation = result.Value.RetrieveBucketAggregation("colorBrands");
            aggregation.ShouldNotBeNull();
            var buckets = aggregation.Buckets.Cast<KeyBucket>().ToList();
            buckets.ShouldAllBe(bucket => bucket.IsComplex);
            buckets.ToDictionary(bucket => string.Join("+", bucket.Keys), bucket => bucket.TotalOfDocuments).ShouldBe(new Dictionary<string, long?>
            {
                ["Red+acme"] = 2,
                ["Red+globex"] = 1,
                ["Dark Blue+acme"] = 1,
                ["Dark Blue+globex"] = 1,
                ["Green+initech"] = 1
            }, ignoreOrder: true);
            buckets[0].Keys.ShouldBe(["Red", "acme"]);
        }
    });

    [Fact]
    public async Task SearchAsync_FilterFacetWithNestedTermFacet_CountsTheFilteredDocuments() => await RunAsync(async index =>
    {
        var filter = new FilterAggregationQuery
        {
            AggregationName = "globex",
            Queries = { [Occurance.MUST] = BrandIs("globex") },
            NestedAggregations = [new TermAggregation { AggregationName = "colors", TermFieldName = Color }]
        };
        var query = new SearchQuery { Index = index, FacetQueries = [filter] };

        var result = await SearchAsync(query);

        if (RanComplex(result))
        {
            var aggregation = result.Value.RetrieveSingleAggregation("globex");
            aggregation.ShouldNotBeNull();
            var bucket = aggregation.Buckets.ShouldHaveSingleItem();
            bucket.TotalOfDocuments.ShouldBe(2);
            CountsByKey(bucket.Aggregations.RetrieveBucketAggregation("colors"))
                .ShouldBe(new Dictionary<string, long?> { ["Red"] = 1, ["Dark Blue"] = 1 }, ignoreOrder: true);
            CountsByKey(aggregation.NestedAggregation.RetrieveBucketAggregation("colors"))
                .ShouldBe(new Dictionary<string, long?> { ["Red"] = 1, ["Dark Blue"] = 1 }, ignoreOrder: true);
            bucket.SubBuckets.Count.ShouldBe(2);
        }
    });

    [Fact]
    public async Task SearchAsync_NestedTermFacet_ReturnsSubBucketsOfEachBucket() => await RunAsync(async index =>
    {
        var query = new SearchQuery
        {
            Index = index,
            FacetQueries =
            [
                new TermAggregation
                {
                    AggregationName = "brands",
                    TermFieldName = Brand,
                    NestedAggregations = [new TermAggregation { AggregationName = "colors", TermFieldName = Color }]
                }
            ]
        };

        var result = await SearchAsync(query);

        if (RanComplex(result))
        {
            var brands = result.Value.RetrieveBucketAggregation("brands");
            Counts(brands).ShouldBe([("acme", 3), ("globex", 2), ("initech", 1)]);
            var colorsByBrand = brands!.Buckets.Cast<KeyBucket>().ToDictionary(
                bucket => bucket.Key,
                bucket => CountsByKey(bucket.Aggregations.RetrieveBucketAggregation("colors")));
            colorsByBrand["acme"].ShouldBe(new Dictionary<string, long?> { ["Red"] = 2, ["Dark Blue"] = 1 }, ignoreOrder: true);
            colorsByBrand["globex"].ShouldBe(new Dictionary<string, long?> { ["Red"] = 1, ["Dark Blue"] = 1 }, ignoreOrder: true);
            colorsByBrand["initech"].ShouldBe(new Dictionary<string, long?> { ["Green"] = 1 }, ignoreOrder: true);
        }
    });

    private sealed class UnknownAggregation : IAggregationQuery
    {
        public string AggregationName { get; set; } = "unknown";
        public List<IAggregationQuery> NestedAggregations { get; set; } = [];
    }

    public static TheoryData<string, IAggregationQuery?[], string> InvalidFacets() => new()
    {
        { "null entry", [null], "FacetQueries holds a null entry." },
        { "no name", [new TermAggregation { TermFieldName = Color }], "A facet needs an AggregationName." },
        { "no field", [new TermAggregation { AggregationName = "colors" }], "Facet colors needs a TermFieldName." },
        { "negative size", [new TermAggregation { AggregationName = "colors", TermFieldName = Color, Size = -1 }], "Facet colors has Size -1" },
        { "size past limit", [new TermAggregation { AggregationName = "colors", TermFieldName = Color, Size = 65_537 }], "past the limit of 65536 buckets" },
        { "same name twice", [new TermAggregation { AggregationName = "x", TermFieldName = Color }, new TermAggregation { AggregationName = "x", TermFieldName = Brand }], "Facet name x is used more than once" },
        { "one multi-term field", [new MultiTermAggreation { AggregationName = "pair", Terms = [new SimpleTerm { TermFieldName = Color }] }], "Facet pair needs at least two Terms" },
        { "unknown type", [new UnknownAggregation()], "Facet unknown is a UnknownAggregation" },
        {
            "unknown nested type",
            [new TermAggregation { AggregationName = "colors", TermFieldName = Color, NestedAggregations = [new UnknownAggregation()] }],
            "Facet unknown is a UnknownAggregation"
        },
        { "nested in itself", [NestedInItself()], "Facet colors is nested inside itself." },
        { "nested too deep", [NestedLevels(17)], "Facet level17 is nested past the limit of 16 levels." }
    };

    private static TermAggregation NestedInItself()
    {
        var facet = new TermAggregation { AggregationName = "colors", TermFieldName = Color };
        facet.NestedAggregations = [new TermAggregation { AggregationName = "brands", TermFieldName = Brand, NestedAggregations = [facet] }];
        return facet;
    }

    private static TermAggregation NestedLevels(int levels)
    {
        var facet = new TermAggregation { AggregationName = $"level{levels}", TermFieldName = Color };
        for (var level = levels - 1; level >= 1; level--)
        {
            facet = new TermAggregation { AggregationName = $"level{level}", TermFieldName = Color, NestedAggregations = [facet] };
        }

        return facet;
    }

    /// <summary>A malformed facet fails before the engine is asked, so the index doesn't have to exist.</summary>
    [Theory]
    [MemberData(nameof(InvalidFacets))]
    public async Task SearchAsync_InvalidFacet_ReturnsFailedResult(string scenario, IAggregationQuery?[] facets, string message)
    {
        var query = new SearchQuery { Index = $"facets-{Guid.NewGuid():N}", FacetQueries = facets.ToList()! };

        var result = await SearchAsync(query);

        result.IsFailed.ShouldBeTrue(scenario);
        result.Errors.Single().Message.ShouldContain(UnsupportedFacets ?? message);
    }
}
