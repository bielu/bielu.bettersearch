using System.Diagnostics;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Diagnostics;
using Bielu.BetterSearch.Services;
using FluentResults;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Bielu.BetterSearch.Core.Tests;

public class SearchServiceTelemetryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string NewIndex() => $"index-{Guid.NewGuid():N}";

    [Fact]
    public async Task SearchAsync_Succeeds_RecordsSpanWithRequestAndResultTags()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        var provider = new FakeSearchProvider { Search = (_, _) => Task.FromResult(Result.Ok<ISearchResult>(FakeSearchProvider.Results(3, 42))) };
        using var service = new SearchServiceAsync(provider);

        var result = await service.SearchAsync(new SearchQuery { Index = index, Page = 2, PageSize = 3 }, Token);

        result.IsSuccess.ShouldBeTrue();
        var activity = capture.SingleActivity();
        activity.OperationName.ShouldBe($"search {index}");
        activity.Kind.ShouldBe(ActivityKind.Client);
        activity.Status.ShouldBe(ActivityStatusCode.Unset);
        activity.GetTagItem(BetterSearchTelemetry.OperationTag).ShouldBe("search");
        activity.GetTagItem(BetterSearchTelemetry.ProviderTag).ShouldBe(nameof(FakeSearchProvider));
        activity.GetTagItem(BetterSearchTelemetry.OutcomeTag).ShouldBe(BetterSearchTelemetry.Success);
        activity.GetTagItem(BetterSearchTelemetry.PageTag).ShouldBe(2);
        activity.GetTagItem(BetterSearchTelemetry.PageSizeTag).ShouldBe(3);
        activity.GetTagItem(BetterSearchTelemetry.HitCountTag).ShouldBe(3);
        activity.GetTagItem(BetterSearchTelemetry.TotalCountTag).ShouldBe(42L);
    }

    [Fact]
    public async Task SearchAsync_ProviderFails_SetsErrorStatusWithFirstMessageAndLogsWarning()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        var provider = new FakeSearchProvider
        {
            Search = (_, _) => Task.FromResult(Result.Fail<ISearchResult>("first problem").WithError("second problem")),
        };
        var logger = new RecordingLogger<SearchServiceAsync>();
        using var service = new SearchServiceAsync(provider, logger);

        var result = await service.SearchAsync(new SearchQuery { Index = index }, Token);

        result.IsFailed.ShouldBeTrue();
        var activity = capture.SingleActivity();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.StatusDescription.ShouldBe("first problem");
        activity.GetTagItem(BetterSearchTelemetry.OutcomeTag).ShouldBe(BetterSearchTelemetry.Failure);
        activity.GetTagItem(BetterSearchTelemetry.HitCountTag).ShouldBeNull();
        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldContain("first problem");
        entry.Message.ShouldContain(index);
    }

    [Fact]
    public async Task SearchAsync_QueryHasValues_RecordsNoQueryValue()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        using var service = new SearchServiceAsync(new FakeSearchProvider());
        var query = new SearchQuery { Index = index };
        query.Add(Occurance.MUST, new TermSubQuery { Field = "secretField", Value = "secret-value" });

        await service.SearchAsync(query, Token);

        var activity = capture.SingleActivity();
        foreach (var tag in activity.TagObjects)
        {
            (tag.Value?.ToString() ?? string.Empty).ShouldNotContain("secret");
        }

        activity.OperationName.ShouldNotContain("secret");
    }

    [Fact]
    public async Task SearchAsync_Cancelled_RecordsCancelledOutcomeAndRethrows()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        var logger = new RecordingLogger<SearchServiceAsync>();
        using var service = new SearchServiceAsync(new FakeSearchProvider(), logger);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => service.SearchAsync(new SearchQuery { Index = index }, cancelled.Token));

        var activity = capture.SingleActivity();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(BetterSearchTelemetry.OutcomeTag).ShouldBe(BetterSearchTelemetry.Cancelled);
        capture.Measurements("bettersearch.operations").ShouldHaveSingleItem()
            .Tags[BetterSearchTelemetry.OutcomeTag].ShouldBe(BetterSearchTelemetry.Cancelled);
        logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ProviderThrows_LogsErrorAndRethrows()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        var provider = new FakeSearchProvider { Search = (_, _) => throw new InvalidOperationException("engine exploded") };
        var logger = new RecordingLogger<SearchServiceAsync>();
        using var service = new SearchServiceAsync(provider, logger);

        await Should.ThrowAsync<InvalidOperationException>(() => service.SearchAsync(new SearchQuery { Index = index }, Token));

        var activity = capture.SingleActivity();
        activity.StatusDescription.ShouldBe("engine exploded");
        activity.GetTagItem(BetterSearchTelemetry.OutcomeTag).ShouldBe(BetterSearchTelemetry.Exception);
        activity.GetTagItem(BetterSearchTelemetry.ErrorTypeTag).ShouldBe(typeof(InvalidOperationException).FullName);
        logger.Entries.ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Error);
    }

    [Fact]
    public async Task SearchAsync_TwoCalls_RecordsDurationAndCountByOutcome()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        var fail = false;
        var provider = new FakeSearchProvider
        {
            Search = (_, _) => Task.FromResult(fail ? Result.Fail<ISearchResult>("no") : Result.Ok<ISearchResult>(FakeSearchProvider.Results(0, 0))),
        };
        using var service = new SearchServiceAsync(provider);

        await service.SearchAsync(new SearchQuery { Index = index }, Token);
        fail = true;
        await service.SearchAsync(new SearchQuery { Index = index }, Token);

        capture.Measurements("bettersearch.operations")
            .Select(m => (string?)m.Tags[BetterSearchTelemetry.OutcomeTag])
            .ShouldBe([BetterSearchTelemetry.Success, BetterSearchTelemetry.Failure]);
        var durations = capture.Measurements("bettersearch.operation.duration");
        durations.Count.ShouldBe(2);
        durations.ShouldAllBe(m => m.Value >= 0 && (string?)m.Tags[BetterSearchTelemetry.OperationTag] == "search");
    }

    [Fact]
    public async Task SearchAsync_SingleUseHits_EnumeratesHitsOnceAndTagsHitCount()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        var hits = new SingleUseEnumerableDecorator(FakeSearchProvider.Results(3, 3).Items);
        var provider = new FakeSearchProvider
        {
            Search = (_, _) => Task.FromResult(Result.Ok<ISearchResult>(new BaseSearchResults { Items = hits, Pagination = new Pagination { TotalCount = 3 } })),
        };
        using var service = new SearchServiceAsync(provider);
        var notified = new List<ISearchModel>();
        using var subscription = service.Subscribe(new RecordingObserver(notified));

        var result = await service.SearchAsync(new SearchQuery { Index = index }, Token);

        result.IsSuccess.ShouldBeTrue();
        hits.Enumerations.ShouldBe(1);
        notified.Count.ShouldBe(3);
        capture.SingleActivity().GetTagItem(BetterSearchTelemetry.HitCountTag).ShouldBe(3);
    }

    [Fact]
    public async Task SearchAsync_NoListener_StillReturnsProviderResult()
    {
        var provider = new FakeSearchProvider { Search = (_, _) => Task.FromResult(Result.Ok<ISearchResult>(FakeSearchProvider.Results(2, 2))) };
        using var service = new SearchServiceAsync(provider);

        var result = await service.SearchAsync(new SearchQuery { Index = NewIndex() }, Token);

        result.Value.Items.Count().ShouldBe(2);
    }

    /// <summary>Hits that throw when enumerated a second time, as a lazy provider sequence can.</summary>
    private sealed class SingleUseEnumerableDecorator(IEnumerable<ISearchHit> hits) : IEnumerable<ISearchHit>
    {
        public int Enumerations { get; private set; }

        public IEnumerator<ISearchHit> GetEnumerator()
        {
            Enumerations++;
            Enumerations.ShouldBe(1, "the hits were enumerated more than once");
            return hits.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class RecordingObserver(List<ISearchModel> notified) : IObserver<ISearchModel>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(ISearchModel value) => notified.Add(value);
    }
}
