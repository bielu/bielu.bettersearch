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

    [Fact]
    public async Task SearchAsync_TermForWholeDecimal_MatchesNothing()
    {
        // Lifti indexes "3.5" as the words "3" and "5", so the whole value is never one word. The feature
        // matrix documents this; the test fails if Lifti starts matching it, so the docs get updated.
        var index = $"culture-{Guid.NewGuid():N}";
        var indexed = await indexingService.IndexDocumentAsync(new SearchDocument
        {
            Id = "1",
            Index = index,
            Fields = new Dictionary<string, IList<object>> { { "Price", [3.5] } }
        }, TestContext.Current.CancellationToken);
        indexed.IsSuccess.ShouldBeTrue();

        var query = new SearchQuery { Index = index };
        query.Add(Occurance.MUST, new TermSubQuery { Field = "Price", Value = 3.5 });
        var result = await searchService.SearchAsync(query, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        result.Value.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_FuzzyForDecimal_MatchesNearbyInteger()
    {
        // "3.5" is two edits from the word "35", while the decimal itself is indexed as "3" and "5". The
        // feature matrix documents this; the test fails if Lifti changes it, so the docs get updated.
        var index = $"culture-{Guid.NewGuid():N}";
        foreach (var (id, price) in new[] { ("decimal", 3.5), ("integer", 35d) })
        {
            var indexed = await indexingService.IndexDocumentAsync(new SearchDocument
            {
                Id = id,
                Index = index,
                Fields = new Dictionary<string, IList<object>> { { "Price", [price] } }
            }, TestContext.Current.CancellationToken);
            indexed.IsSuccess.ShouldBeTrue();
        }

        var query = new SearchQuery { Index = index };
        query.Add(Occurance.MUST, new FuzzyQuery { Field = "Price", Value = 3.5 });
        var result = await searchService.SearchAsync(query, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        result.Value.Items.Select(item => item.Document.Id).ShouldBe(["integer"]);
    }
}
