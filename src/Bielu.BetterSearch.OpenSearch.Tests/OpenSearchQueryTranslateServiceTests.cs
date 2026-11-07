using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using OpenSearch.Client;
using OpenSearch.Net;
using Shouldly;
using DateRangeQuery = Bielu.BetterSearch.Abstractions.Query.SubQueries.DateRangeQuery;
using FuzzyQuery = Bielu.BetterSearch.Abstractions.Query.SubQueries.FuzzyQuery;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public class OpenSearchQueryTranslateServiceTests(
    IQueryTranslateServiceAsync<QueryContainer> translateService,
    IPostFilterTranslateServiceAsync<QueryContainer> postFilterTranslateService,
    IEnumerable<ISubQueryTranslator<QueryContainer>> translators)
{
    [Fact]
    public async Task TranslateMainQueryAsync_NoClauses_ReturnsMatchAll()
    {
        var result = await translateService.TranslateMainQueryAsync(new SearchQuery());

        result.IsSuccess.ShouldBeTrue();
        ((IQueryContainer)result.Value).MatchAll.ShouldNotBeNull();
    }

    [Fact]
    public async Task TranslateMainQueryAsync_Clauses_ReturnsBoolQueryWithEachOccurrence()
    {
        var query = new SearchQuery();
        query.Add(Occurance.MUST, new TermSubQuery { Field = "Name", Value = "jane" });
        query.Add(Occurance.SHOULD, new TermSubQuery { Field = "City", Value = "warsaw" });
        query.Add(Occurance.MUSTNOT, new TermSubQuery { Field = "Age", Value = 30 });
        query.Add(Occurance.FILTER, new PrefixSubQuery { Field = "Name", Value = "ja" });

        var result = await translateService.TranslateMainQueryAsync(query);

        result.IsSuccess.ShouldBeTrue();
        var boolQuery = ((IQueryContainer)result.Value).Bool;
        boolQuery.ShouldNotBeNull();
        boolQuery.Must.Count().ShouldBe(1);
        boolQuery.Should.Count().ShouldBe(1);
        boolQuery.MustNot.Count().ShouldBe(1);
        boolQuery.Filter.Count().ShouldBe(1);
    }

    public static TheoryData<ISearchSubQuery> ConditionlessSubQueries() => new()
    {
        new TermSubQuery { Field = "City.keyword", Value = "" },
        new FuzzyQuery { Field = "Name", Value = "" },
        new DateRangeQuery { Field = "Born" },
        new NumericRange { Field = "Age" },
        new StringRange { Field = "Name" },
    };

    [Theory]
    [MemberData(nameof(ConditionlessSubQueries))]
    public async Task TranslateMainQueryAsync_ConditionlessClause_IsStillSent(ISearchSubQuery subQuery)
    {
        var query = new SearchQuery();
        query.Add(Occurance.MUST, subQuery);

        var result = await translateService.TranslateMainQueryAsync(query);

        result.IsSuccess.ShouldBeTrue();
        var json = new OpenSearchClient().RequestResponseSerializer.SerializeToString(new SearchRequest("people") { Query = result.Value });
        json.ShouldContain("\"must\":[{");
    }

    [Fact]
    public async Task TranslateMainQueryAsync_SubQueryWithoutField_ReturnsFailedResult()
    {
        var query = new SearchQuery();
        query.Add(Occurance.MUST, new TermSubQuery { Value = "jane" });

        var result = await translateService.TranslateMainQueryAsync(query);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("Field");
    }

    [Fact]
    public async Task TranslatePostFilterAsync_NoPostFilter_ReturnsNull()
    {
        var result = await postFilterTranslateService.TranslatePostFilterAsync(new SearchQuery());

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    [Theory]
    [InlineData("Name", "fields.Name")]
    [InlineData("fields.Name", "fields.Name")]
    [InlineData("_id", "_id")]
    [InlineData("_tags", "fields._tags")]
    public async Task TermSubQuery_FieldName_IsResolvedUnderFields(string field, string expected)
    {
        var subQuery = new TermSubQuery { Field = field, Value = "jane" };

        var result = await translators.First(t => t.CanTranslate(subQuery)).TranslateAsync(subQuery, translators);

        result.IsSuccess.ShouldBeTrue();
        var term = ((IQueryContainer)result.Value).Term;
        term.Field.Name.ShouldBe(expected);
        term.CaseInsensitive.ShouldBe(true);
    }

    [Fact]
    public async Task FuzzyQuery_EditDistanceAboveTwo_IsClampedToTwo()
    {
        var subQuery = new Bielu.BetterSearch.Abstractions.Query.SubQueries.FuzzyQuery { Field = "Name", Value = "jnae", Fuziness = Tuple.Create(1, 5) };

        var result = await translators.First(t => t.CanTranslate(subQuery)).TranslateAsync(subQuery, translators);

        result.IsSuccess.ShouldBeTrue();
        var match = ((IQueryContainer)result.Value).Match;
        match.Fuzziness.EditDistance.ShouldBe(2);
        match.Operator.ShouldBe(Operator.And);
    }

    [Fact]
    public async Task TermsSubQuery_StringValues_AlsoSendsThemLowerCased()
    {
        var subQuery = new TermsSubQuery { Field = "City", Value = ["Warsaw", "krakow", 5] };

        var result = await translators.First(t => t.CanTranslate(subQuery)).TranslateAsync(subQuery, translators);

        result.IsSuccess.ShouldBeTrue();
        ((IQueryContainer)result.Value).Terms.Terms.ShouldBe(["Warsaw", "warsaw", "krakow", 5], ignoreOrder: true);
    }

    [Fact]
    public async Task SpatialSearchQuery_InvalidUnit_ReturnsFailedResult()
    {
        var subQuery = new SpatialSearchQuery { Field = "Location", Distance = 10, UnitOfDistance = "parsecs" };

        var result = await translators.First(t => t.CanTranslate(subQuery)).TranslateAsync(subQuery, translators);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("UnitOfDistance");
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-90.5, 0)]
    [InlineData(0, 200)]
    [InlineData(0, -180.5)]
    [InlineData(double.NaN, 0)]
    public async Task SpatialSearchQuery_CoordinatesOutOfRange_ReturnsFailedResult(double lat, double lng)
    {
        var subQuery = new SpatialSearchQuery { Field = "Location", Distance = 10, Lat = lat, Lng = lng };

        var result = await translators.First(t => t.CanTranslate(subQuery)).TranslateAsync(subQuery, translators);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain(lat is >= -90 and <= 90 ? "Lng" : "Lat");
    }

    [Theory]
    [InlineData(0.00001)]
    [InlineData(1e20)]
    public async Task SpatialSearchQuery_VerySmallOrLargeDistance_IsAccepted(double distance)
    {
        var subQuery = new SpatialSearchQuery { Field = "Location", Distance = distance, Lat = 52.23, Lng = 21.01 };

        var result = await translators.First(t => t.CanTranslate(subQuery)).TranslateAsync(subQuery, translators);

        result.IsSuccess.ShouldBeTrue();
        var geoDistance = ((IQueryContainer)result.Value).GeoDistance.Distance;
        geoDistance.Precision.ShouldBe(distance);
        geoDistance.Unit.ShouldBe(DistanceUnit.Kilometers);
    }

    [Fact]
    public async Task DateRangeQuery_UtcBound_IsSentAsUtc()
    {
        var subQuery = new DateRangeQuery { Field = "Born", MinValue = new DateTime(2020, 5, 1, 12, 0, 0, DateTimeKind.Utc) };

        var json = await TranslateToJsonAsync(subQuery);

        json.ShouldContain("\"gt\":\"2020-05-01T12:00:00Z\"");
    }

    [Fact]
    public async Task DateRangeQuery_LocalBound_KeepsTheLocalOffset()
    {
        var bound = new DateTime(2020, 5, 1, 12, 0, 0, DateTimeKind.Local);
        var subQuery = new DateRangeQuery { Field = "Born", MinValue = bound };

        var json = await TranslateToJsonAsync(subQuery);

        var offset = new DateTimeOffset(bound).ToString("zzz", System.Globalization.CultureInfo.InvariantCulture);
        json.ShouldContain($"\"gt\":\"2020-05-01T12:00:00{offset}\"");
    }

    private async Task<string> TranslateToJsonAsync(ISearchSubQuery subQuery)
    {
        var result = await translators.First(t => t.CanTranslate(subQuery)).TranslateAsync(subQuery, translators);
        result.IsSuccess.ShouldBeTrue();
        return new OpenSearchClient().RequestResponseSerializer.SerializeToString(new SearchRequest("people") { Query = result.Value });
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task DateRangeQuery_Edges_PickInclusiveOrExclusiveBounds(bool includeMin, bool includeMax)
    {
        var subQuery = new Bielu.BetterSearch.Abstractions.Query.SubQueries.DateRangeQuery
        {
            Field = "Born",
            MinValue = new DateTime(1985, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            MaxValue = new DateTime(1995, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IncludeMinEdge = includeMin,
            IncludeMaxEdge = includeMax
        };

        var result = await translators.First(t => t.CanTranslate(subQuery)).TranslateAsync(subQuery, translators);

        result.IsSuccess.ShouldBeTrue();
        var range = (IDateRangeQuery)((IQueryContainer)result.Value).Range;
        (range.GreaterThanOrEqualTo != null).ShouldBe(includeMin);
        (range.GreaterThan != null).ShouldBe(!includeMin);
        (range.LessThanOrEqualTo != null).ShouldBe(includeMax);
        (range.LessThan != null).ShouldBe(!includeMax);
    }
}
