using System.Globalization;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Services;
using FluentResults;
using Lucene.Net.Search;
using Microsoft.Extensions.Options;
using FuzzyQuery = Lucene.Net.Search.FuzzyQuery;
using LuceneQuery = Lucene.Net.Search.Query;

namespace Bielu.BetterSearch.Meilisearch.Services.Queries;

/// <summary>
/// Parses the query with Lucene's own parser, as Elasticsearch's <c>query_string</c> does, and translates the tree into
/// one filter. A field name may carry the <c>fields.</c> prefix Elasticsearch needs or leave it out. A term matches one
/// word of the field, as <see cref="TermSubQueryTranslator"/> does; a trailing <c>*</c> prefix, a range,
/// <c>_exists_:field</c>, <c>field:*</c> and <c>AND</c>, <c>OR</c>, <c>NOT</c>, <c>+</c> and <c>-</c> translate exactly.
/// A phrase of several words and a fuzzy term (<c>~</c>) translate approximately and add a
/// <see cref="TranslationWarning"/>. A term without a field,
/// proximity, a boost, a regular expression and any other wildcard return a failed <c>Result</c>.
/// </summary>
internal sealed class LuceneSubQueryTranslator(IOptionsMonitor<MeilisearchOptions> optionsMonitor)
    : MeilisearchSubQueryTranslator<LuceneSubQuery>
{
    private const string QueryName = nameof(LuceneSubQuery);
    private const string ExistsField = "_exists_";

    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd", "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mmK", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"
    ];

    protected override Result<MeilisearchFilter> Translate(LuceneSubQuery query, IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators)
    {
        var parsed = LuceneQueryParser.Parse(query.LuceneQuery, QueryName);
        if (parsed.IsFailed)
        {
            return parsed.ToResult<MeilisearchFilter>();
        }

        var warnings = new List<ISuccess>();
        var filter = Translate(parsed.Value, warnings);
        return filter.IsFailed ? filter : filter.WithSuccesses(warnings);
    }

    private Result<MeilisearchFilter> Translate(LuceneQuery query, List<ISuccess> warnings)
    {
        if (query.Boost != 1f)
        {
            return Fail($"boosts such as '{query}' only change the score, which Meilisearch filters do not compute.");
        }

        return query switch
        {
            BooleanQuery boolQuery => Bool(boolQuery, warnings),
            MatchAllDocsQuery => Filter(MeilisearchFieldNames.MatchAll),
            TermQuery term => Term(term.Term.Field, term.Term.Text),
            FuzzyQuery fuzzy => Fuzzy(fuzzy, warnings),
            PrefixQuery prefix => Prefix(prefix.Prefix.Field, prefix.Prefix.Text),
            WildcardQuery wildcard => Wildcard(wildcard.Term.Field, wildcard.Term.Text),
            PhraseQuery phrase => Phrase(phrase, warnings),
            TermRangeQuery range => Range(range),
            RegexpQuery => Fail($"regular expressions such as '{query}' have no Meilisearch filter."),
            _ => Fail($"'{query}' ({query.GetType().Name}) has no Meilisearch filter.")
        };
    }

    /// <summary>The clauses joined as <see cref="MeilisearchBoolFilterBuilder"/> joins a bool query's.</summary>
    private Result<MeilisearchFilter> Bool(BooleanQuery query, List<ISuccess> warnings)
    {
        var required = new List<string>();
        var should = new List<string>();
        var mustNot = new List<string>();
        foreach (var clause in query.Clauses)
        {
            var translated = Translate(clause.Query, warnings);
            if (translated.IsFailed)
            {
                return translated;
            }

            var target = clause.Occur switch
            {
                Occur.SHOULD => should,
                Occur.MUST_NOT => mustNot,
                _ => required
            };
            target.Add(translated.Value.Expression);
        }

        return Result.Ok(MeilisearchBoolFilterBuilder.Combine(required, should, mustNot));
    }

    private static Result<MeilisearchFilter> Term(string queryField, string text)
    {
        if (queryField == ExistsField)
        {
            return Exists(text);
        }

        var field = Field(queryField, text);
        if (field.IsFailed)
        {
            return field.ToResult<MeilisearchFilter>();
        }

        // A number or boolean is compared as given, as TermSubQuery does, also against the stored value.
        if (TermValues.Coerce(text) != null)
        {
            return TermValues.Filter(field.Value, [text], QueryName);
        }

        return MeilisearchDocument.Tokenize(text) switch
        {
            [var word] => TermValues.Filter(field.Value, [word], QueryName),
            [] => Fail($"'{queryField}:{text}' has no word to match."),
            _ => Fail($"'{text}' is several words for Meilisearch; write \"{text}\" to match them as a phrase.")
        };
    }

    private static Result<MeilisearchFilter> Fuzzy(FuzzyQuery query, List<ISuccess> warnings)
    {
        var term = Term(query.Term.Field, query.Term.Text);
        if (term.IsSuccess)
        {
            warnings.Add(new TranslationWarning(
                $"{QueryName} fuzzy term '{query}' matches the exact word on Meilisearch: its filters have no edit distance."));
        }

        return term;
    }

    /// <summary>A word of the field starting with the prefix, ignoring case, as <see cref="PrefixSubQueryTranslator"/> matches.</summary>
    private static Result<MeilisearchFilter> Prefix(string queryField, string prefix)
    {
        var field = Field(queryField, prefix + "*");
        if (field.IsFailed)
        {
            return field.ToResult<MeilisearchFilter>();
        }

        if (prefix.Length == 0)
        {
            return Exists(field.Value);
        }

        return MeilisearchDocument.Tokenize(prefix) is [var word] && word == prefix.ToLowerInvariant()
            ? Filter($"{MeilisearchFieldNames.In(MeilisearchFieldNames.Tokens, field.Value)} STARTS WITH {MeilisearchFieldNames.Quote(word)}")
            : Fail($"prefix '{prefix}*' is not one word, so it cannot start a word Meilisearch stores.");
    }

    /// <summary><c>field:*</c> is <c>_exists_:field</c>, as on Elasticsearch; Meilisearch filters match no other wildcard.</summary>
    private static Result<MeilisearchFilter> Wildcard(string queryField, string pattern)
    {
        if (pattern == "*")
        {
            var field = Field(queryField, pattern);
            return field.IsFailed ? field.ToResult<MeilisearchFilter>() : Exists(field.Value);
        }

        return Fail($"wildcard '{pattern}' has no Meilisearch filter: only a trailing '*', as in jo*, translates.");
    }

    private static Result<MeilisearchFilter> Exists(string queryField)
    {
        var field = MeilisearchFieldNames.Require(StripFieldsPrefix(queryField), QueryName);
        return field.IsFailed
            ? field.ToResult<MeilisearchFilter>()
            : Filter($"{MeilisearchFieldNames.In(MeilisearchFieldNames.Fields, field.Value)} EXISTS");
    }

    /// <summary>
    /// A run of words the field stores in <c>phrases</c> equal to the phrase, or starting with it while the field also
    /// holds its last word. Meilisearch trims a trailing space from a filter value, so <c>STARTS WITH "jane do "</c>
    /// also matches <c>jane doe</c>; the last word check removes most such hits, but not all, so this adds a
    /// <see cref="TranslationWarning"/>. The phrase can have at most <see cref="MeilisearchDocument.MaxPhraseTokens"/> words.
    /// </summary>
    private Result<MeilisearchFilter> Phrase(PhraseQuery query, List<ISuccess> warnings)
    {
        var terms = query.GetTerms();
        if (terms.Length == 0)
        {
            return Fail($"phrase '{query}' has no words.");
        }

        if (query.Slop != 0)
        {
            return Fail($"proximity '{query}' has no Meilisearch filter: phrases match only words next to each other.");
        }

        var words = terms.SelectMany(term => MeilisearchDocument.Tokenize(term.Text)).ToList();
        if (words.Count == 1)
        {
            return Term(terms[0].Field, words[0]);
        }

        var field = Field(terms[0].Field, $"\"{string.Join(' ', terms.Select(term => term.Text))}\"");
        if (field.IsFailed)
        {
            return field.ToResult<MeilisearchFilter>();
        }

        if (words.Count == 0)
        {
            return Fail($"phrase '{query}' has no words.");
        }

        if (words.Count > MeilisearchDocument.MaxPhraseTokens)
        {
            return Fail($"phrase '{query}' has {words.Count} words; Meilisearch matches phrases of at most {MeilisearchDocument.MaxPhraseTokens}.");
        }

        if (optionsMonitor.CurrentValue.PhraseFields is { } phraseFields && !phraseFields.Contains(field.Value))
        {
            return Fail($"phrase field '{field.Value}' is not in MeilisearchOptions.PhraseFields, so its phrases are not stored.");
        }

        var attribute = MeilisearchFieldNames.In(MeilisearchFieldNames.Phrases, field.Value);
        var phrase = MeilisearchFieldNames.Quote(string.Join(' ', words));
        var lastWord = $"{MeilisearchFieldNames.In(MeilisearchFieldNames.Tokens, field.Value)} = {MeilisearchFieldNames.Quote(words[^1])}";
        warnings.Add(new TranslationWarning(
            $"{QueryName} phrase '{query}' matches a run of words starting with it while the field also holds the word '{words[^1]}' anywhere: Meilisearch filters cannot require a run to end there."));
        return Filter($"{attribute} = {phrase} OR ({attribute} STARTS WITH {phrase} AND {lastWord})");
    }

    /// <summary>
    /// A range over the field's words, as Elasticsearch compares a text field. Bounds that read as numbers also compare
    /// numbers, and bounds that read as ISO 8601 dates also compare dates, as on a numeric or date field. A date without
    /// a time rounds as Elasticsearch rounds it: up to the end of the day for an included maximum or an excluded minimum.
    /// </summary>
    private static Result<MeilisearchFilter> Range(TermRangeQuery query)
    {
        var lower = query.LowerTerm?.Utf8ToString();
        var upper = query.UpperTerm?.Utf8ToString();
        var field = Field(query.Field, query.ToString());
        if (field.IsFailed)
        {
            return field.ToResult<MeilisearchFilter>();
        }

        if (lower == null && upper == null)
        {
            return Exists(field.Value);
        }

        var bounds = new BaseRangeQuery { Field = field.Value, IncludeMinEdge = query.IncludesLower, IncludeMaxEdge = query.IncludesUpper };
        var filters = new List<string>();
        var words = StringRangeSubQueryTranslator.Create(new StringRange
        {
            Field = field.Value,
            MinValue = lower,
            MaxValue = upper,
            IncludeMinEdge = query.IncludesLower,
            IncludeMaxEdge = query.IncludesUpper
        });
        if (words.IsFailed)
        {
            return words;
        }

        filters.Add(words.Value.Expression);
        if (ToNumber(lower, out var minNumber) && ToNumber(upper, out var maxNumber))
        {
            filters.Add(RangeFilter.Numbers(bounds, QueryName, MeilisearchFieldNames.Fields, minNumber, maxNumber).Value.Expression);
        }
        else if (ToDate(lower, !query.IncludesLower, out var minDate) && ToDate(upper, query.IncludesUpper, out var maxDate))
        {
            filters.Add(RangeFilter.Numbers(bounds, QueryName, MeilisearchFieldNames.Dates, minDate, maxDate).Value.Expression);
        }

        return filters.Count == 1 ? words : Filter(string.Join(" OR ", filters.Select(filter => $"({filter})")));
    }

    /// <summary>Whether <paramref name="text"/> is absent or a finite number.</summary>
    private static bool ToNumber(string? text, out double? number)
    {
        number = null;
        if (text == null)
        {
            return true;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value))
        {
            number = value;
            return true;
        }

        return false;
    }

    /// <summary>Whether <paramref name="text"/> is absent or an ISO 8601 date, as Unix milliseconds; UTC without a time zone.</summary>
    private static bool ToDate(string? text, bool roundUp, out double? milliseconds)
    {
        milliseconds = null;
        if (text == null)
        {
            return true;
        }

        if (!DateTimeOffset.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
        {
            return false;
        }

        var value = date.ToUnixTimeMilliseconds();
        if (roundUp && text.Length == "yyyy-MM-dd".Length)
        {
            value += (long)TimeSpan.FromDays(1).TotalMilliseconds - 1;
        }

        milliseconds = value;
        return true;
    }

    /// <summary>The field a clause names, without the <c>fields.</c> prefix Elasticsearch needs.</summary>
    private static Result<string> Field(string queryField, string text) =>
        queryField == LuceneQueryParser.NoField
            ? Result.Fail<string>(
                $"{QueryName} term '{text}' names no field. Meilisearch filters compare one field at a time, so write field:{text}.")
            : MeilisearchFieldNames.Require(StripFieldsPrefix(queryField), QueryName);

    private static string StripFieldsPrefix(string field) =>
        MeilisearchFieldNames.IsStoredValue(field) ? field[MeilisearchFieldNames.FieldsPrefix.Length..] : field;

    private static Result<MeilisearchFilter> Fail(string reason) => Result.Fail<MeilisearchFilter>($"{QueryName}: {reason}");
}
