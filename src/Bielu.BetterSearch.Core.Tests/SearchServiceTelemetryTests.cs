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

internal sealed class NotFoundError(string message) : Error(message);

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
    public async Task SearchAsync_ProviderFails_SetsErrorStatusAndTypeWithoutMessageAndLogsWarning()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        var provider = new FakeSearchProvider
        {
            Search = (_, _) => Task.FromResult(Result.Fail<ISearchResult>(new NotFoundError("first problem")).WithError("second problem")),
        };
        var logger = new RecordingLogger<SearchServiceAsync>();
        using var service = new SearchServiceAsync(provider, logger);

        var result = await service.SearchAsync(new SearchQuery { Index = index }, Token);

        result.IsFailed.ShouldBeTrue();
        var activity = capture.SingleActivity();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.StatusDescription.ShouldBeNull();
        activity.GetTagItem(BetterSearchTelemetry.ErrorTypeTag).ShouldBe(typeof(NotFoundError).FullName);
        activity.GetTagItem(BetterSearchTelemetry.OutcomeTag).ShouldBe(BetterSearchTelemetry.Failure);
        activity.GetTagItem(BetterSearchTelemetry.HitCountTag).ShouldBeNull();
        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldNotContain("problem");
        entry.Message.ShouldContain(typeof(NotFoundError).FullName!);
        entry.Message.ShouldContain(index);
    }

    [Fact]
    public async Task SearchAsync_ProviderFailsWithRecordErrorMessages_RecordsFirstMessage()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        var provider = new FakeSearchProvider
        {
            Search = (_, _) => Task.FromResult(Result.Fail<ISearchResult>("first problem").WithError("second problem")),
        };
        var logger = new RecordingLogger<SearchServiceAsync>();
        using var service = new SearchServiceAsync(provider, logger, new InstrumentationOptionsMonitor(recordErrorMessages: true));

        await service.SearchAsync(new SearchQuery { Index = index }, Token);

        capture.SingleActivity().StatusDescription.ShouldBe("first problem");
        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.Message.ShouldContain("first problem");
        entry.Message.ShouldNotContain("second problem");
    }

    [Fact]
    public async Task SearchAsync_InstrumentationOptionsUnreadable_LeavesMessagesOutAndReturnsProviderResult()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        var provider = new FakeSearchProvider { Search = (_, _) => Task.FromResult(Result.Fail<ISearchResult>("secret problem")) };
        using var service = new SearchServiceAsync(provider, null, new InstrumentationOptionsMonitor(recordErrorMessages: true, throws: true));

        var result = await service.SearchAsync(new SearchQuery { Index = index }, Token);

        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("secret problem");
        capture.SingleActivity().StatusDescription.ShouldBeNull();
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
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.StatusDescription.ShouldBeNull();
        activity.GetTagItem(BetterSearchTelemetry.OutcomeTag).ShouldBe(BetterSearchTelemetry.Exception);
        activity.GetTagItem(BetterSearchTelemetry.ErrorTypeTag).ShouldBe(typeof(InvalidOperationException).FullName);
        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Error);
        entry.Exception.ShouldBeNull();
        entry.Message.ShouldNotContain("engine exploded");
        entry.Message.ShouldContain(typeof(InvalidOperationException).FullName!);
    }

    [Fact]
    public async Task SearchAsync_ProviderThrowsWithRecordErrorMessages_RecordsMessageAndLogsException()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        var exception = new InvalidOperationException("engine exploded");
        var provider = new FakeSearchProvider { Search = (_, _) => throw exception };
        var logger = new RecordingLogger<SearchServiceAsync>();
        using var service = new SearchServiceAsync(provider, logger, new InstrumentationOptionsMonitor(recordErrorMessages: true));

        await Should.ThrowAsync<InvalidOperationException>(() => service.SearchAsync(new SearchQuery { Index = index }, Token));

        capture.SingleActivity().StatusDescription.ShouldBe("engine exploded");
        logger.Entries.ShouldHaveSingleItem().Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public async Task GetDocumentAsync_Found_RecordsGetSpanWithOneHit()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        var provider = new FakeSearchProvider { GetDocumentResult = Result.Ok<SearchDocument?>(new SearchDocument { Id = "1", Index = index }) };
        using var service = new SearchServiceAsync(provider);

        (await service.GetDocumentAsync(index, "1", Token)).Value.ShouldNotBeNull();

        var activity = capture.SingleActivity();
        activity.OperationName.ShouldBe($"get {index}");
        activity.GetTagItem(BetterSearchTelemetry.OperationTag).ShouldBe("get");
        activity.GetTagItem(BetterSearchTelemetry.OutcomeTag).ShouldBe(BetterSearchTelemetry.Success);
        activity.GetTagItem(BetterSearchTelemetry.HitCountTag).ShouldBe(1);
        capture.Measurements("bettersearch.operations").ShouldHaveSingleItem()
            .Tags[BetterSearchTelemetry.OperationTag].ShouldBe("get");
    }

    [Fact]
    public async Task GetDocumentAsync_Missing_RecordsSuccessWithNoHit()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        using var service = new SearchServiceAsync(new FakeSearchProvider());

        (await service.GetDocumentAsync(index, "1", Token)).Value.ShouldBeNull();

        var activity = capture.SingleActivity();
        activity.GetTagItem(BetterSearchTelemetry.OutcomeTag).ShouldBe(BetterSearchTelemetry.Success);
        activity.GetTagItem(BetterSearchTelemetry.HitCountTag).ShouldBe(0);
    }

    [Fact]
    public async Task GetDocumentAsync_EmptyId_RecordsFailedGetSpan()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        using var service = new SearchServiceAsync(new FakeSearchProvider());

        (await service.GetDocumentAsync(index, "", Token)).IsFailed.ShouldBeTrue();

        var activity = capture.SingleActivity();
        activity.GetTagItem(BetterSearchTelemetry.OperationTag).ShouldBe("get");
        activity.GetTagItem(BetterSearchTelemetry.OutcomeTag).ShouldBe(BetterSearchTelemetry.Failure);
    }

    [Fact]
    public async Task CountAsync_Succeeds_RecordsCountSpanWithTheCount()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        using var service = new SearchServiceAsync(new FakeSearchProvider { CountResult = Result.Ok(7L) });

        (await service.CountAsync(new SearchQuery { Index = index }, Token)).Value.ShouldBe(7L);

        var activity = capture.SingleActivity();
        activity.OperationName.ShouldBe($"count {index}");
        activity.GetTagItem(BetterSearchTelemetry.OperationTag).ShouldBe("count");
        activity.GetTagItem(BetterSearchTelemetry.TotalCountTag).ShouldBe(7L);
        capture.Measurements("bettersearch.operations").ShouldHaveSingleItem()
            .Tags[BetterSearchTelemetry.OutcomeTag].ShouldBe(BetterSearchTelemetry.Success);
    }

    [Fact]
    public async Task CountAsync_ProviderFails_RecordsFailedCountSpan()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        using var service = new SearchServiceAsync(new FakeSearchProvider { CountResult = Result.Fail<long>("count rejected") });

        (await service.CountAsync(new SearchQuery { Index = index }, Token)).IsFailed.ShouldBeTrue();

        var activity = capture.SingleActivity();
        activity.GetTagItem(BetterSearchTelemetry.OperationTag).ShouldBe("count");
        activity.GetTagItem(BetterSearchTelemetry.OutcomeTag).ShouldBe(BetterSearchTelemetry.Failure);
        activity.GetTagItem(BetterSearchTelemetry.TotalCountTag).ShouldBeNull();
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
