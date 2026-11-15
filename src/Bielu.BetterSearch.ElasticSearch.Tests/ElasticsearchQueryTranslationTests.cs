using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport;
using Elastic.Transport.Extensions;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public sealed class ElasticsearchQueryTranslationTests(IEnumerable<ISubQueryTranslator<Query>> translators)
    : SubQueryTranslationTestBase<Query>(translators)
{
    private static readonly Serializer Serializer = new ElasticsearchClient().RequestResponseSerializer;

    protected override string Render(Query translated) => Serializer.SerializeToString(translated);

    public static TheoryData<double, string> BoostsOutsideFloatRange() => new()
    {
        { 1e100, "1E+100" },
        { 1e-100, "1E-100" }
    };

    // A 32-bit float turns 1e100 into infinity, which the engine rejects, and 1e-100 into 0, which drops the clause's score.
    [Theory]
    [MemberData(nameof(BoostsOutsideFloatRange))]
    public async Task TranslateAsync_BoostOutsideFloatRange_ReturnsFailedResult(double boost, string written)
    {
        var result = await TranslateAsync(new TermSubQuery { Field = "Name", Value = "jane", BoostValue = boost });

        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe(
            $"TermSubQuery has a BoostValue {written} that Elasticsearch can't hold as a 32-bit float; use a boost between 1E-45 and 3.4028235E+38.");
    }

    [Theory]
    [MemberData(nameof(BoostsOutsideFloatRange))]
    public async Task TranslateAsync_BoostOutsideFloatRangeInNestedBool_ReturnsFailedResult(double boost, string written)
    {
        var nested = new BoolSearchSubQuery { BoostValue = boost };
        nested.Add(Occurrence.Must, new TermSubQuery { Field = "Name", Value = "jane" });
        var outer = new BoolSearchSubQuery();
        outer.Add(Occurrence.Should, nested);

        var result = await TranslateAsync(outer);

        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldStartWith($"BoolSearchSubQuery has a BoostValue {written} that Elasticsearch");
    }

    [Fact]
    public async Task TranslateAsync_LargestFloatBoost_IsSent()
    {
        var result = await TranslateAsync(new TermSubQuery { Field = "Name", Value = "jane", BoostValue = float.MaxValue });

        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        Render(result.Value).ShouldContain("\"boost\":3.4028");
    }
}
