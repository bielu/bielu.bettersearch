using System.Diagnostics;
using Bielu.BetterSearch.Diagnostics;
using Bielu.BetterSearch.Services;
using FluentResults;
using Shouldly;

namespace Bielu.BetterSearch.Core.Tests;

public class IndexingServiceTelemetryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string NewIndex() => $"index-{Guid.NewGuid():N}";

    private static SearchDocument Document(string index, string id) => new() { Id = id, Type = index, Index = index };

    [Fact]
    public async Task IndexDocumentAsync_Succeeds_RecordsIndexSpanAndOneIndexedDocument()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        using var service = new IndexingServiceAsync(new FakeIndexingProvider(), new AcceptingValidator());

        (await service.IndexDocumentAsync(Document(index, "1"), Token)).IsSuccess.ShouldBeTrue();

        var activity = capture.SingleActivity();
        activity.OperationName.ShouldBe($"index {index}");
        activity.GetTagItem(BetterSearchTelemetry.ProviderTag).ShouldBe(nameof(FakeIndexingProvider));
        capture.Measurements("bettersearch.documents.indexed").ShouldHaveSingleItem().Value.ShouldBe(1);
    }

    [Fact]
    public async Task IndexDocumentAsync_NullValue_RecordsFailedSpanAndNothingIndexed()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        using var service = new IndexingServiceAsync(new FakeIndexingProvider(), new AcceptingValidator());
        var document = Document(index, "1");
        document.Fields["title"] = [null!];

        (await service.IndexDocumentAsync(document, Token)).IsFailed.ShouldBeTrue();

        var activity = capture.SingleActivity();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.StatusDescription!.ShouldContain("null values in fields (title)");
        capture.Measurements("bettersearch.documents.indexed").ShouldBeEmpty();
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_Succeeds_TagsDocumentCountAndCountsIndexedDocuments()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        var provider = new FakeIndexingProvider { BulkIndexResult = Result.Ok(3) };
        using var service = new IndexingServiceAsync(provider, new AcceptingValidator());

        var result = await service.IndexMultipleDocumentsAsync([Document(index, "1"), Document(index, "2"), Document(index, "3")], Token);

        result.Value.ShouldBe(3);
        var activity = capture.SingleActivity();
        activity.OperationName.ShouldBe($"bulk index {index}");
        activity.GetTagItem(BetterSearchTelemetry.DocumentCountTag).ShouldBe(3);
        capture.Measurements("bettersearch.documents.indexed").ShouldHaveSingleItem().Value.ShouldBe(3);
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_TwoIndexes_RecordsNoIndexTag()
    {
        var index = NewIndex();
        var other = NewIndex();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == BetterSearchInstrumentation.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        var stopped = new List<Activity>();
        listener.ActivityStopped = activity =>
        {
            if (Equals(activity.GetTagItem(BetterSearchTelemetry.DocumentCountTag), 2) && activity.GetTagItem(BetterSearchTelemetry.ProviderTag) is "TwoIndexProvider")
            {
                lock (stopped)
                {
                    stopped.Add(activity);
                }
            }
        };
        ActivitySource.AddActivityListener(listener);
        using var service = new IndexingServiceAsync(new TwoIndexProvider(), new AcceptingValidator());

        await service.IndexMultipleDocumentsAsync([Document(index, "1"), Document(other, "2")], Token);

        var activity = stopped.ShouldHaveSingleItem();
        activity.OperationName.ShouldBe("bulk index");
        activity.GetTagItem(BetterSearchTelemetry.IndexTag).ShouldBeNull();
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_ProviderFails_CountsNothingIndexed()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        var provider = new FakeIndexingProvider { BulkIndexResult = Result.Fail<int>("bulk rejected") };
        using var service = new IndexingServiceAsync(provider, new AcceptingValidator());

        await service.IndexMultipleDocumentsAsync([Document(index, "1")], Token);

        capture.SingleActivity().StatusDescription.ShouldBe("bulk rejected");
        capture.Measurements("bettersearch.documents.indexed").ShouldBeEmpty();
        capture.Measurements("bettersearch.operations").ShouldHaveSingleItem()
            .Tags[BetterSearchTelemetry.OutcomeTag].ShouldBe(BetterSearchTelemetry.Failure);
    }

    [Fact]
    public async Task RemoveDocumentAsync_Succeeds_RecordsRemoveSpanAndOneRemovedDocument()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        using var service = new IndexingServiceAsync(new FakeIndexingProvider(), new AcceptingValidator());

        await service.RemoveDocumentAsync(new DeleteDocumentRequest { Id = "1", Type = index }, Token);

        capture.SingleActivity().OperationName.ShouldBe($"remove {index}");
        capture.Measurements("bettersearch.documents.removed").ShouldHaveSingleItem().Value.ShouldBe(1);
    }

    [Fact]
    public async Task RemoveDocumentsAsync_Succeeds_CountsRemovedDocumentsFromProvider()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        var provider = new FakeIndexingProvider { RemoveDocumentsResult = Result.Ok(2) };
        using var service = new IndexingServiceAsync(provider, new AcceptingValidator());

        var result = await service.RemoveDocumentsAsync(index, ["1", "2", "3"], Token);

        result.Value.ShouldBe(2);
        var activity = capture.SingleActivity();
        activity.OperationName.ShouldBe($"bulk remove {index}");
        activity.GetTagItem(BetterSearchTelemetry.DocumentCountTag).ShouldBe(2);
        capture.Measurements("bettersearch.documents.removed").ShouldHaveSingleItem().Value.ShouldBe(2);
    }

    [Fact]
    public async Task RemoveAllDocumentsAsync_CountReported_CountsRemovedDocuments()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        var provider = new FakeIndexingProvider { RemoveAllResult = Result.Ok<long?>(7) };
        using var service = new IndexingServiceAsync(provider, new AcceptingValidator());

        await service.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = index }, Token);

        var activity = capture.SingleActivity();
        activity.OperationName.ShouldBe($"remove all {index}");
        activity.GetTagItem(BetterSearchTelemetry.DocumentCountTag).ShouldBe(7L);
        capture.Measurements("bettersearch.documents.removed").ShouldHaveSingleItem().Value.ShouldBe(7);
    }

    [Fact]
    public async Task RemoveAllDocumentsAsync_CountNotReported_RecordsSpanButNoRemovedCount()
    {
        var index = NewIndex();
        using var capture = new TelemetryCapture(index);
        using var service = new IndexingServiceAsync(new FakeIndexingProvider(), new AcceptingValidator());

        (await service.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = index }, Token)).IsSuccess.ShouldBeTrue();

        capture.SingleActivity().GetTagItem(BetterSearchTelemetry.OutcomeTag).ShouldBe(BetterSearchTelemetry.Success);
        capture.Measurements("bettersearch.documents.removed").ShouldBeEmpty();
    }

    /// <summary>Its own type name, so the listener of the two-index test picks out its span.</summary>
    private sealed class TwoIndexProvider : FakeIndexingProvider;
}
