using System.Text.Json;
using Bielu.BetterSearch.Abstractions.Caching;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using FluentResults;
using Shouldly;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Bielu.BetterSearch.Caching.Tests;

/// <summary>A cached result must come back from a distributed (L2) cache's serializer with every value's type intact.</summary>
public class CachedSearchResultsTests
{
    private static readonly DateTime Utc = new(2026, 11, 8, 13, 5, 0, 123, DateTimeKind.Utc);
    private static readonly DateTime Local = new(2026, 11, 8, 13, 5, 0, DateTimeKind.Local);
    private static readonly DateTime Unspecified = new(2026, 11, 8, 13, 5, 0, DateTimeKind.Unspecified);

    private static Result<ISearchResult> Sample()
    {
        using var json = JsonDocument.Parse("""{"lat":52.2,"lon":21.0}""");
        var document = new BaseSearchModel
        {
            Id = "doc-1",
            ContentType = "page",
            CustomProperties = new Dictionary<string, List<object>>
            {
                ["big"] = [(1L << 53) + 1],
                ["int"] = [7],
                ["double"] = [3.0],
                ["tiny"] = [double.Epsilon],
                ["huge"] = [double.MaxValue],
                ["float"] = [1.25f],
                ["decimal"] = [12.345m],
                ["dates"] = [Utc, Local, Unspecified],
                ["offset"] = [new DateTimeOffset(2026, 11, 8, 13, 5, 0, TimeSpan.FromHours(2))],
                ["day"] = [new DateOnly(2026, 11, 8)],
                ["flags"] = [true, false],
                ["text"] = ["Żółw 3,5"],
                ["empty"] = [],
                ["nulls"] = [null!],
                ["point"] = [json.RootElement.Clone()]
            }
        };
        var highlights = new Dictionary<string, IReadOnlyList<string>> { ["Name"] = ["<em>Jane</em> Doe"] };
        ISearchResult search = new BaseSearchResults
        {
            Items = [new SearchHit(document, double.NaN, highlights), new SearchHit(new BaseSearchModel { Id = "doc-2" }, null)],
            Pagination = new Pagination { TotalCount = 12, Page = 2, PageSize = 10, TotalPages = 2 }
        };
        return Result.Ok(search).WithSuccess(new TranslationWarning("approximate"));
    }

    private static Result<ISearchResult> RoundTrip(Result<ISearchResult> result)
    {
        var cached = CachedSearchResults.From(result);
        cached.ShouldNotBeNull();
        var serializer = new FusionCacheSystemTextJsonSerializer();
        var bytes = serializer.Serialize(cached);
        return CachedSearchResults.ToResult(serializer.Deserialize<CachedSearchResult>(bytes)!);
    }

    [Fact]
    public void ToResult_ThroughL2Serializer_KeepsEveryValueAndType()
    {
        //Act
        var result = RoundTrip(Sample());

        //Assert
        var hits = result.Value.Items.ToList();
        hits.Count.ShouldBe(2);
        var fields = hits[0].Document.CustomProperties;
        fields["big"].Single().ShouldBeOfType<long>().ShouldBe((1L << 53) + 1);
        fields["int"].Single().ShouldBeOfType<int>().ShouldBe(7);
        fields["double"].Single().ShouldBeOfType<double>().ShouldBe(3.0);
        fields["tiny"].Single().ShouldBe(double.Epsilon);
        fields["huge"].Single().ShouldBe(double.MaxValue);
        fields["float"].Single().ShouldBeOfType<float>().ShouldBe(1.25f);
        fields["decimal"].Single().ShouldBeOfType<decimal>().ShouldBe(12.345m);
        var dates = fields["dates"].Cast<DateTime>().ToList();
        dates.ShouldBe([Utc, Local, Unspecified]);
        dates.Select(date => date.Kind).ShouldBe([DateTimeKind.Utc, DateTimeKind.Local, DateTimeKind.Unspecified]);
        fields["offset"].Single().ShouldBeOfType<DateTimeOffset>().Offset.ShouldBe(TimeSpan.FromHours(2));
        fields["day"].Single().ShouldBe(new DateOnly(2026, 11, 8));
        fields["flags"].ShouldBe([true, false]);
        fields["text"].Single().ShouldBe("Żółw 3,5");
        fields["empty"].ShouldBeEmpty();
        fields["nulls"].ShouldBe([null!]);
        fields["point"].Single().ShouldBeOfType<JsonElement>().GetProperty("lat").GetDouble().ShouldBe(52.2);
        hits[0].Document.Id.ShouldBe("doc-1");
        hits[0].Document.ContentType.ShouldBe("page");
        double.IsNaN(hits[0].Score!.Value).ShouldBeTrue();
        hits[0].Highlights["Name"].ShouldBe(["<em>Jane</em> Doe"]);
        hits[1].Score.ShouldBeNull();
        hits[1].Highlights.ShouldBeEmpty();
        hits[1].Document.CustomProperties.ShouldBeEmpty();
        result.Value.Pagination.TotalCount.ShouldBe(12);
        result.Value.Pagination.Page.ShouldBe(2);
        result.Value.Pagination.PageSize.ShouldBe(10);
        result.Value.Pagination.TotalPages.ShouldBe(2);
        result.Successes.OfType<TranslationWarning>().Single().Message.ShouldBe("approximate");
    }

    [Fact]
    public void From_ValueOfUnsupportedType_ReturnsNull()
    {
        var document = new BaseSearchModel { Id = "1", CustomProperties = { ["id"] = [Guid.Empty] } };
        ISearchResult search = new BaseSearchResults { Items = [new SearchHit(document, null)], Pagination = new Pagination() };

        CachedSearchResults.From(Result.Ok(search)).ShouldBeNull();
    }
}
