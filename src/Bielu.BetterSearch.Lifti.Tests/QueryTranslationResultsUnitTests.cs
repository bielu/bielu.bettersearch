using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Lifti.Services;
using Bielu.BetterSearch.Tests;
using Shouldly;

namespace Bielu.BetterSearch.Lifti.Tests;

public class QueryTranslationResultsUnitTests(
    ISearchServiceAsync serviceAsync,
    IIndexingServiceAsync indexingServiceAsync,
    IIndexingProviderAsync indexingProviderAsync,
    ILiftiIndexManager indexManager)
    : QueryResultBaseTests(serviceAsync, indexingServiceAsync, indexingProviderAsync)
{
    /// <summary>Lifti's hits hold only the document id.</summary>
    protected override bool ReturnsStoredFields => false;

    /// <summary>Lifti has no query for every document, so the ids come from the index itself.</summary>
    protected override async Task<IReadOnlyCollection<string>> IndexedIdsAsync(string index) =>
        (await indexManager.GetIndexAsync(index)).Value?.Metadata.GetIndexedDocuments().Select(document => document.Key).ToList() ?? [];

    /// <summary>Lifti has no query for every document, so it cannot return the complement.</summary>
    [Fact]
    public override async Task SearchAsync_MustNotOnly_ReturnsTheComplement() => await SearchAsync_MustNotOnly_ShouldFailAsync();

    /// <summary>The <c>MUST</c> only excludes, so the matches are every other document, which Lifti cannot return.</summary>
    [Fact]
    public override async Task SearchAsync_ShouldBesideMustOfNestedMustNotOnly_ReturnsEveryDocumentExceptTheExclusions() =>
        await SearchAsync_ShouldBesideMustOfNestedMustNotOnly_ShouldFailAsync();

    /// <summary>Lifti does not support ranges, so the range fails rather than matching the wrong documents.</summary>
    [Fact]
    public override async Task SearchAsync_RangeWithDefaultEdges_IncludesBothBounds() => await SearchAsync_RangeWithDefaultEdges_ShouldFailAsync();

    /// <summary>Known failure: searching a missing index creates it; <c>LiftiSearchTests</c> pins that behavior.</summary>
    [Fact(Skip = "Lifti creates a missing index on search (ROADMAP.md, Confirmed defects: \"Searching a missing index creates it\")")]
    public override async Task SearchAsync_MissingIndex_ReturnsFailedResult() =>
        await base.SearchAsync_MissingIndex_ReturnsFailedResult();

    /// <summary>Known failure: Lifti ignores <c>Field</c> on a term query, so the filter also matches the title holding the culture value.</summary>
    [Fact(Skip = "Lifti ignores Field on a term query (ROADMAP.md, Confirmed defects: \"`Field` is ignored\")")]
    public override async Task SearchAsync_FluentCultureFilter_ReturnsOnlyThatCulture() =>
        await base.SearchAsync_FluentCultureFilter_ReturnsOnlyThatCulture();

    /// <summary>
    /// Known defect: Lifti ignores <c>Field</c> on a term query (ROADMAP.md, Confirmed defects: "`Field` is ignored"),
    /// so the filter also matches the title holding the culture value. The other cultures are still left out.
    /// </summary>
    [Fact]
    public async Task SearchAsync_FluentCultureFilter_AlsoMatchesTheCultureInOtherFields()
    {
        var ids = await SearchFluentCultureFilterAsync();

        ids.ShouldBe(["1", "2"]);
    }
}
