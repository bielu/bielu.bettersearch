using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Meilisearch.Services;
using Bielu.BetterSearch.Meilisearch.Services.Queries;
using FluentResults;
using Shouldly;
using MeilisearchQuery = Meilisearch.SearchQuery;

namespace Bielu.BetterSearch.Meilisearch.Tests;

/// <summary>Pins how Lucene syntax translates; the search tests check what the filters match on a real server.</summary>
public class MeilisearchLuceneSubQueryTests(IQueryTranslateServiceAsync<MeilisearchQuery> translateService)
{
    private Task<Result<MeilisearchQuery>> TranslateAsync(string lucene, Occurance occurance = Occurance.MUST)
    {
        var query = new SearchQuery();
        query.Add(occurance, new LuceneSubQuery { LuceneQuery = lucene });
        return translateService.TranslateMainQueryAsync(query);
    }

    public static TheoryData<string, string> Exact => new()
    {
        { "Name:Jane", "(tokens.Name = \"jane\")" },
        { "fields.Name:Jane", "(tokens.Name = \"jane\")" },
        { "Age:30", "(tokens.Age = \"30\" OR fields.Age = 30)" },
        { "Active:true", "(tokens.Active = \"true\" OR fields.Active = true)" },
        { "Name:\"Jane\"", "(tokens.Name = \"jane\")" },
        { "Name:Jo*", "(tokens.Name STARTS WITH \"jo\")" },
        { "_exists_:City", "(fields.City EXISTS)" },
        { "_exists_:fields.City", "(fields.City EXISTS)" },
        { "City:*", "(fields.City EXISTS)" },
        { "City:[* TO *]", "(fields.City EXISTS)" },
        { "*:*", "(id EXISTS)" },
        { "Age:[30 TO 40]", "((tokens.Age \"30\" TO \"40\") OR (fields.Age 30 TO 40))" },
        { "Age:{30 TO 40}", "((tokens.Age \"30\0\" TO \"4/\U0010FFFF\") OR (fields.Age 30.000000000000004 TO 39.99999999999999))" },
        { "Age:[30 TO *]", "((tokens.Age >= \"30\") OR (fields.Age >= 30))" },
        { "Age:{* TO 30}", "((tokens.Age < \"30\") OR (fields.Age < 30))" },
        { "City:[A TO K]", "(tokens.City \"a\" TO \"k\")" },
        { "Born:[1990-01-01 TO 1999-12-31]", "((tokens.Born \"1990-01-01\" TO \"1999-12-31\") OR (dates.Born 631152000000 TO 946684799999))" },
        { "Born:{1990-01-01 TO *]", "((tokens.Born > \"1990-01-01\") OR (dates.Born > 631238399999))" },
        { "Born:[* TO 2000-01-01T12:00:00Z]", "((tokens.Born <= \"2000-01-01t12:00:00z\") OR (dates.Born <= 946728000000))" },
        { "Name:jane AND City:warsaw", "((tokens.Name = \"jane\") AND (tokens.City = \"warsaw\"))" },
        { "Name:jane OR City:warsaw", "(((tokens.Name = \"jane\") OR (tokens.City = \"warsaw\")))" },
        { "+Name:jane City:warsaw", "((tokens.Name = \"jane\"))" },
        { "NOT Name:jane", "(id EXISTS AND NOT (tokens.Name = \"jane\"))" },
        { "-Name:jane", "(id EXISTS AND NOT (tokens.Name = \"jane\"))" },
        { "Name:jane -(City:warsaw OR City:krakow)", "((tokens.Name = \"jane\") AND NOT (((tokens.City = \"warsaw\") OR (tokens.City = \"krakow\"))))" },
        { "Name:jane AND (NOT City:warsaw)", "((tokens.Name = \"jane\") AND (id EXISTS AND NOT (tokens.City = \"warsaw\")))" },
    };

    [Theory]
    [MemberData(nameof(Exact))]
    public async Task TranslateMainQueryAsync_ExactLuceneSyntax_TranslatesWithoutWarnings(string lucene, string expected)
    {
        var result = await TranslateAsync(lucene);

        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
        ((object)result.Value.Filter).ShouldBe(expected);
        result.Successes.OfType<TranslationWarning>().ShouldBeEmpty();
    }

    public static TheoryData<string, string, string> Approximate => new()
    {
        { "Name:jnae~", "(tokens.Name = \"jnae\")", "fuzzy term 'Name:jnae~2' matches the exact word" },
        {
            "Name:\"Jane Doe\"", "(phrases.Name = \"jane doe\" OR (phrases.Name STARTS WITH \"jane doe\" AND tokens.Name = \"doe\"))",
            "phrase 'Name:\"Jane Doe\"' matches a run of words starting with it"
        },
        { "Name:jnae~1 AND City:warsaw", "((tokens.Name = \"jnae\") AND (tokens.City = \"warsaw\"))", "fuzzy term 'Name:jnae~1' matches the exact word" },
    };

    [Theory]
    [MemberData(nameof(Approximate))]
    public async Task TranslateMainQueryAsync_ApproximateLuceneSyntax_TranslatesWithWarning(string lucene, string expected, string warning)
    {
        var result = await TranslateAsync(lucene);

        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
        ((object)result.Value.Filter).ShouldBe(expected);
        result.Successes.OfType<TranslationWarning>().Single().Message.ShouldContain(warning);
    }

    [Fact]
    public async Task TranslateMainQueryAsync_WarningInPostFilter_IsKept()
    {
        var query = new SearchQuery();
        var nested = new BoolSearchSubQuery();
        nested.Add(Occurance.MUST, new LuceneSubQuery { LuceneQuery = "Name:jnae~" });
        query.PostFilterQuery[Occurance.MUST] = [nested];

        var result = await translateService.TranslateMainQueryAsync(query);

        result.Successes.OfType<TranslationWarning>().Single().Message.ShouldContain("fuzzy term");
    }

    public static TheoryData<string, string> Unsupported => new()
    {
        { "jane", "term 'jane' names no field" },
        { "Name:jane OR doe", "term 'doe' names no field" },
        { "\"jane doe\"", "names no field" },
        { "Name:\"jane doe\"~2", "proximity" },
        { "Name:jane^2", "boosts" },
        { "Name:/ja.e/", "regular expressions" },
        { "Name:j?ne", "wildcard 'j?ne'" },
        { "Name:*ane*", "wildcard '*ane*'" },
        { "Name:jo-h*", "prefix 'jo-h*' is not one word" },
        { "Name:foo-bar", "'foo-bar' is several words" },
        { "Name:(jane AND", "is not valid Lucene syntax" },
        { "Name\\ x:jane", "field 'Name x' can only contain" },
        { "  ", "needs a query" },
        { $"Name:\"{string.Join(' ', Enumerable.Repeat("word", 17))}\"", "has 17 words" },
    };

    [Theory]
    [MemberData(nameof(Unsupported))]
    public async Task TranslateMainQueryAsync_UnsupportedLuceneSyntax_ReturnsFailureNamingIt(string lucene, string message)
    {
        var result = await TranslateAsync(lucene);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain(message);
    }

    [Fact]
    public async Task TranslateAsync_PhraseOutsidePhraseFields_ReturnsFailure()
    {
        var translator = new LuceneSubQueryTranslator(new StaticOptionsMonitor(new MeilisearchOptions { PhraseFields = ["Title"] }));

        var inside = await translator.TranslateAsync(new LuceneSubQuery { LuceneQuery = "Title:\"a b\"" }, [translator]);
        var outside = await translator.TranslateAsync(new LuceneSubQuery { LuceneQuery = "Name:\"a b\"" }, [translator]);

        inside.IsSuccess.ShouldBeTrue();
        outside.Errors.Single().Message.ShouldContain("PhraseFields");
    }
}
