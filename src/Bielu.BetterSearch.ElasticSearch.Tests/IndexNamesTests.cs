using Bielu.BetterSearch.Services;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

/// <summary>The shared index naming, linked into the Elasticsearch and OpenSearch providers.</summary>
public class IndexNamesTests
{
    private static readonly DateTimeOffset Started = new(2026, 11, 14, 13, 45, 12, 345, TimeSpan.FromHours(2));

    [Fact]
    public void NewRebuildTarget_Index_IsTheNameTheUtcTimeAndASuffix()
    {
        var target = IndexNames.NewRebuildTarget("app-people", Started);

        target.ShouldStartWith("app-people-rebuild-20261114114512345-");
        target.Length.ShouldBe("app-people-rebuild-20261114114512345-".Length + 8);
        IndexNames.IsRebuildTargetOf("app-people", target).ShouldBeTrue();
    }

    [Fact]
    public void NewRebuildTarget_SameTime_GivesDifferentNames() =>
        IndexNames.NewRebuildTarget("people", Started).ShouldNotBe(IndexNames.NewRebuildTarget("people", Started));

    [Theory]
    [InlineData("people", null)]
    [InlineData("people", "people")]
    [InlineData("people", "other")]
    [InlineData("people", "people-rebuild-")]
    [InlineData("people", "people-rebuild-2026111411451234-0123abcd")]
    [InlineData("people", "people-rebuild-20261114114512345-0123ABCD")]
    [InlineData("people", "people-rebuild-x-rebuild-20261114114512345-0123abcd")]
    [InlineData("app-people", "people-rebuild-20261114114512345-0123abcd")]
    public void IsRebuildTargetOf_NotARebuildOfTheIndex_IsFalse(string index, string? target) =>
        IndexNames.IsRebuildTargetOf(index, target).ShouldBeFalse();
}
