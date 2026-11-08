using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Shouldly;

namespace Bielu.BetterSearch.Lifti.Tests;

public class QueryTranslationResultsUnitTests(ISearchServiceAsync serviceAsync, IIndexingServiceAsync indexingServiceAsync) : QueryResultBaseTests(serviceAsync, indexingServiceAsync)
{
    /// <summary>Lifti has no query for every document, so it cannot return the complement.</summary>
    [Fact]
    public override async Task SearchAsync_MustNotOnly_ReturnsTheComplement() => await SearchAsync_MustNotOnly_ShouldFailAsync();

    /// <summary>The <c>MUST</c> only excludes, so the matches are every other document, which Lifti cannot return.</summary>
    [Fact]
    public override async Task SearchAsync_ShouldBesideMustOfNestedMustNotOnly_ReturnsEveryDocumentExceptTheExclusions() =>
        await SearchAsync_ShouldBesideMustOfNestedMustNotOnly_ShouldFailAsync();

    /// <summary>
    /// Known defect: Lifti ignores <c>Field</c> on a term query (ROADMAP.md, Confirmed defects: "`Field` is ignored"),
    /// so the filter also matches the title holding the culture value. The other cultures are still left out.
    /// </summary>
    [Fact]
    public override async Task SearchAsync_FluentCultureFilter_ReturnsOnlyThatCulture()
    {
        var ids = await SearchFluentCultureFilterAsync();

        ids.ShouldBe(["1", "2"]);
    }
}
