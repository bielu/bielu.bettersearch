using System.Globalization;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Shouldly;

namespace Bielu.BetterSearch.Lifti.Tests;

public class CultureTests(ISearchServiceAsync searchService, IIndexingServiceAsync indexingService)
{
    [Fact]
    public async Task IndexDocumentAsync_CultureWithLetterDecimalSeparator_IndexesInvariantText()
    {
        var index = $"culture-{Guid.NewGuid():N}";
        var culture = CultureInfo.CurrentCulture;
        // Lifti splits words on punctuation, so a comma decimal separator can't show in a match. A letter
        // separator keeps the culture's "3x5" as one word, while the invariant "3.5" indexes "3" and "5".
        var letterSeparator = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        letterSeparator.NumberFormat.NumberDecimalSeparator = "x";
        try
        {
            CultureInfo.CurrentCulture = letterSeparator;
            var indexed = await indexingService.IndexDocumentAsync(new SearchDocument
            {
                Id = "1",
                Index = index,
                Fields = new Dictionary<string, IList<object>> { { "Price", [3.5] } }
            }, TestContext.Current.CancellationToken);
            indexed.IsSuccess.ShouldBeTrue();

            var query = new SearchQuery { Index = index };
            query.Add(Occurance.MUST, new TermSubQuery { Field = "Price", Value = 5 });
            var result = await searchService.SearchAsync(query, TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
            result.Value.Items.Select(item => item.Document.Id).ShouldBe(["1"]);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}
