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
    public async Task TranslateAsync_TermWithNoValue_ReturnsFailedResult()
    {
        var result = await new TermSubQueryTranslator().TranslateAsync(new TermSubQuery { Field = "Name" }, translators);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("holds no words");
    }

    public static TheoryData<ISearchSubQuery, ISubQueryTranslator<IQuery>> PunctuationOnlySubQueries() => new()
    {
        { new TermSubQuery { Field = "Name", Value = "-" }, new TermSubQueryTranslator() },
        { new TermsSubQuery { Field = "Name", Value = ["jane", "--"] }, new TermsSubQueryTranslator() },
        { new FuzzyQuery { Field = "Name", Value = "." }, new FuzzySubQueryTranslator() },
        { new PrefixSubQuery { Field = "Name", Value = "/" }, new PrefixSubQueryTranslator() },
        { new PrefixPhraseSubQuery { Field = "Name", Value = " " }, new PrefixPhraseSubQueryTranslator() }
    };

    [Theory]
    [MemberData(nameof(PunctuationOnlySubQueries))]
    public async Task TranslateAsync_ValueWithNoWords_ReturnsFailedResult(ISearchSubQuery query, ISubQueryTranslator<IQuery> translator)
    {
        // The index keeps no word for punctuation or spaces, so such a value could never match.
        var result = await translator.TranslateAsync(query, translators);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("holds no words");
    }

    [Fact]
    public async Task TranslateAsync_TermOfManyWords_MatchesThemNextToEachOtherInOrder()
    {
        var result = await new TermSubQueryTranslator().TranslateAsync(new TermSubQuery { Field = "Name", Value = "jane doe" }, translators);

        result.Value.Root.ShouldBeOfType<AdjacentWordsQueryOperator>().Words
            .Select(word => word.ShouldBeOfType<ExactWordQueryPart>().Word).ShouldBe(["JANE", "DOE"]);
    }

    [Fact]
    public async Task TranslateAsync_TermWithRepeatedWords_KeepsEveryWordInOrder()
    {
        // The tokenizer returns a repeated word once, with all its positions; each position is a word of the query.
        var result = await new TermSubQueryTranslator().TranslateAsync(new TermSubQuery { Field = "Born", Value = "00:12:00" }, translators);

        result.Value.Root.ShouldBeOfType<AdjacentWordsQueryOperator>().Words
            .Select(word => word.ShouldBeOfType<ExactWordQueryPart>().Word).ShouldBe(["00", "12", "00"]);
    }

    [Fact]
    public async Task TranslateAsync_TermWithAccents_FoldsThemAsTheIndexDoes()
    {
        var result = await new TermSubQueryTranslator().TranslateAsync(new TermSubQuery { Field = "Name", Value = "Żółw" }, translators);

        result.Value.Root.ShouldBeOfType<ExactWordQueryPart>().Word.ShouldBe("ZOLW");
    }

    [Fact]
    public async Task TranslateAsync_PrefixPhraseOfManyWords_MatchesTheLastWordAsAPrefix()
    {
        var result = await new PrefixPhraseSubQueryTranslator().TranslateAsync(new PrefixPhraseSubQuery { Field = "Name", Value = "jane d" }, translators);

        var words = result.Value.Root.ShouldBeOfType<AdjacentWordsQueryOperator>().Words;
        words.Count.ShouldBe(2);
        words[0].ShouldBeOfType<ExactWordQueryPart>().Word.ShouldBe("JANE");
        words[1].ShouldBeOfType<WildcardQueryPart>();
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
