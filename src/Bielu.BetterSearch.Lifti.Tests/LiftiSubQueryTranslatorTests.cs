using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Lifti.Services.Queries;
using Lifti.Querying;
using Lifti.Querying.QueryParts;
using Shouldly;

namespace Bielu.BetterSearch.Lifti.Tests;

/// <summary>Checks each Lifti translator directly, for the paths a search through the service never reaches.</summary>
public sealed class LiftiSubQueryTranslatorTests(IEnumerable<ISubQueryTranslator<IQuery>> translators)
{
    private sealed class UnknownSubQuery : ISearchSubQuery
    {
        public Occurance Occurance { get; set; }
        public int BoostValue { get; set; }
    }

    public static TheoryData<ISubQueryTranslator<IQuery>, string> SupportedTranslators() => new()
    {
        { new BooleanSubQueryTranslator(), "boolean" },
        { new TermSubQueryTranslator(), "term" },
        { new TermsSubQueryTranslator(), "terms" },
        { new FuzzySubQueryTranslator(), "fuzzy" },
        { new PrefixSubQueryTranslator(), "prefix" },
        { new PrefixPhraseSubQueryTranslator(), "prefix phrase" },
        { new LuceneSubQueryTranslator(), "Lucene" }
    };

    [Theory]
    [MemberData(nameof(SupportedTranslators))]
    public async Task TranslateAsync_OtherQueryType_ReturnsFailedResult(ISubQueryTranslator<IQuery> translator, string name)
    {
        var query = new UnknownSubQuery();

        translator.CanTranslate(query).ShouldBeFalse();
        var result = await translator.TranslateAsync(query, translators);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe($"Query is not a {name} query");
    }

    [Fact]
    public async Task TranslateAsync_EmptyBool_ReturnsTheEmptyQuery()
    {
        var result = await new BooleanSubQueryTranslator().TranslateAsync(new BoolSearchSubQuery(), translators);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Root.ShouldBe(EmptyQueryPart.Instance);
    }

    [Fact]
    public async Task TranslateAsync_BoolWithOnlyMustNot_ReturnsFailedResult()
    {
        var query = new BoolSearchSubQuery();
        query.Add(Occurance.MUSTNOT, new TermSubQuery { Field = "Name", Value = "jane" });

        var result = await new BooleanSubQueryTranslator().TranslateAsync(query, translators);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("MUSTNOT");
    }

    [Fact]
    public async Task TranslateAsync_BoolWithUnsupportedClause_ReturnsFailedResult()
    {
        var query = new BoolSearchSubQuery();
        query.Add(Occurance.MUST, new TermSubQuery { Field = "Name", Value = "jane" });
        query.Add(Occurance.MUST, new LuceneSubQuery { LuceneQuery = "Name:jane" });

        var result = await new BooleanSubQueryTranslator().TranslateAsync(query, translators);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("Lucene");
    }

    [Fact]
    public async Task TranslateAsync_TermWithNoValue_MatchesTheEmptyWord()
    {
        var result = await new TermSubQueryTranslator().TranslateAsync(new TermSubQuery { Field = "Name" }, translators);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Root.ShouldBeOfType<ExactWordQueryPart>().Word.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task TranslateAsync_Term_UpperCasesTheValue()
    {
        var result = await new TermSubQueryTranslator().TranslateAsync(new TermSubQuery { Field = "Name", Value = "jane" }, translators);

        result.Value.Root.ShouldBeOfType<ExactWordQueryPart>().Word.ShouldBe("JANE");
    }

    [Fact]
    public async Task TranslateAsync_TermsWithManyValues_CombinesThemWithOr()
    {
        var result = await new TermsSubQueryTranslator().TranslateAsync(new TermsSubQuery { Field = "Name", Value = ["jane", "john"] }, translators);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Root.ShouldBeOfType<OrQueryOperator>();
    }
}
