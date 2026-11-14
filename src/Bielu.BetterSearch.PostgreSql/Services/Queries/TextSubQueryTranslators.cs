using System.Globalization;
using System.Numerics;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Services;
using FluentResults;
using NpgsqlTypes;

namespace Bielu.BetterSearch.PostgreSql.Services.Queries;

/// <summary>
/// A string matches one word of the field's string values, as the text search configuration splits and normalizes
/// them (lower case with <c>simple</c>), as an Elasticsearch term query matches one analyzed token; a string the
/// configuration splits into several words matches nothing, as in Elasticsearch. A string that reads as a number or
/// boolean also matches that stored value. Numbers, booleans and dates match a stored value equal to them, so
/// <c>3</c> matches <c>3.0</c>, and an integer is compared exactly at any size.
/// </summary>
internal sealed class TermSubQueryTranslator : PostgreSqlSubQueryTranslator<TermSubQuery>
{
    protected override Result<PostgreSqlFilter> Translate(TermSubQuery query) =>
        TermValues.Filter(query.Field, [query.Value], nameof(TermSubQuery));
}

/// <summary>Matches any of the values, each as <see cref="TermSubQueryTranslator"/> does. No values match nothing.</summary>
internal sealed class TermsSubQueryTranslator : PostgreSqlSubQueryTranslator<TermsSubQuery>
{
    protected override Result<PostgreSqlFilter> Translate(TermsSubQuery query) =>
        TermValues.Filter(query.Field, query.Value ?? [], nameof(TermsSubQuery));
}

internal static class TermValues
{
    /// <summary>
    /// One condition for every word and one for every stored value, each with a single array parameter, so the
    /// command stays the same size whatever the number of values.
    /// </summary>
    public static Result<PostgreSqlFilter> Filter(string? queryField, IEnumerable<object?> values, string queryName)
    {
        if (string.IsNullOrWhiteSpace(queryField))
        {
            return Result.Fail<PostgreSqlFilter>($"{queryName} needs a Field.");
        }

        var field = queryField;
        var words = new List<string>();
        var stored = new List<string>();
        foreach (var value in values)
        {
            switch (value)
            {
                case null:
                    return Result.Fail<PostgreSqlFilter>($"{queryName} needs a Value.");
                case string text:
                    words.Add(text);
                    if (bool.TryParse(text, out var flag))
                    {
                        stored.Add(flag ? "true" : "false");
                    }
                    // Not a conditional of an integer and a double: it would turn the integer into a double, losing
                    // precision past 2^53. Integers of any size are compared exactly, as they are stored.
                    else if (BigInteger.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                    {
                        stored.Add(integer.ToString(CultureInfo.InvariantCulture));
                    }
                    else if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
                    {
                        stored.Add(PostgreSqlDocument.Fractional(number));
                    }

                    break;
                default:
                    if (value is double or float && !double.IsFinite(Convert.ToDouble(value, CultureInfo.InvariantCulture)))
                    {
                        return Result.Fail<PostgreSqlFilter>($"{queryName} has a value that is not a finite number.");
                    }

                    var json = PostgreSqlDocument.ValueJson(QueryValueText.IsDate(value) ? QueryValueText.From(value) : value, field);
                    if (json.IsFailed)
                    {
                        return json.ToResult<PostgreSqlFilter>();
                    }

                    stored.Add(json.Value);
                    break;
            }
        }

        var conditions = new List<PostgreSqlFilter>();
        if (words.Count > 0)
        {
            var wordArray = words.Distinct(StringComparer.Ordinal).ToArray();
            conditions.Add(new PostgreSqlFilter.Condition($"{queryName} words on {field}", p =>
                $"EXISTS (SELECT 1 FROM unnest({p.Add(wordArray, NpgsqlDbType.Array | NpgsqlDbType.Text)}) v, plainto_tsquery({p.Configuration}, v) q " +
                $"WHERE numnode(q) = 1 AND d.search @@ q AND {p.Words(field)} @@ q)"));
        }

        if (stored.Count > 0)
        {
            var storedArray = stored.Distinct(StringComparer.Ordinal).ToArray();
            conditions.Add(new PostgreSqlFilter.Condition($"{queryName} values on {field}", p =>
                $"EXISTS (SELECT 1 FROM {p.Elements(field)} e WHERE e = ANY({p.Add(storedArray, NpgsqlDbType.Array | NpgsqlDbType.Jsonb)}))"));
        }

        return Result.Ok(conditions.Count switch
        {
            0 => PostgreSqlFilter.MatchNone,
            1 => conditions[0],
            _ => new PostgreSqlFilter.Or(conditions)
        });
    }
}

/// <summary>
/// Matches a word of the field's string values that starts with the value, normalized as the words are, so
/// <c>John</c> matches <c>johnson</c> with <c>simple</c>. A value of several words matches nothing, as no single
/// word can start with it.
/// </summary>
internal sealed class PrefixSubQueryTranslator : PostgreSqlSubQueryTranslator<PrefixSubQuery>
{
    protected override Result<PostgreSqlFilter> Translate(PrefixSubQuery query) =>
        PrefixFilter.Build(query.Field, query.Value, nameof(PrefixSubQuery), phrase: false);
}

/// <summary>
/// Matches the value's words as a phrase, in order and next to each other, with the last one as a prefix, so
/// <c>john do</c> matches <c>John Doe</c>.
/// </summary>
internal sealed class PrefixPhraseSubQueryTranslator : PostgreSqlSubQueryTranslator<PrefixPhraseSubQuery>
{
    protected override Result<PostgreSqlFilter> Translate(PrefixPhraseSubQuery query) =>
        PrefixFilter.Build(query.Field, query.Value, nameof(PrefixPhraseSubQuery), phrase: true);
}

internal static class PrefixFilter
{
    public static Result<PostgreSqlFilter> Build(string? queryField, object? value, string queryName, bool phrase)
    {
        if (string.IsNullOrWhiteSpace(queryField))
        {
            return Result.Fail<PostgreSqlFilter>($"{queryName} needs a Field.");
        }

        if (value is null || QueryValueText.From(value).Trim().Length == 0)
        {
            return Result.Fail<PostgreSqlFilter>($"{queryName} needs a Value.");
        }

        var field = queryField;
        var text = QueryValueText.From(value);
        // The words as a tsquery, with the last one marked as a prefix: 'john' <-> 'do':*. A query of several words
        // for a single prefix, or of no words at all (only punctuation or stop words), is NULL, which matches nothing.
        var toQuery = phrase ? "phraseto_tsquery" : "plainto_tsquery";
        var words = phrase ? "numnode(w) > 0" : "numnode(w) = 1";
        return Result.Ok<PostgreSqlFilter>(new PostgreSqlFilter.Condition($"{queryName} on {field}", p =>
        {
            // Not correlated with the row, so PostgreSQL computes it once, and d.search @@ q stays a condition the
            // GIN index answers. The IS NOT NULL check keeps the condition false rather than NULL when q is NULL.
            var q = $"(SELECT CASE WHEN {words} THEN (w::text || ':*')::tsquery END FROM {toQuery}({p.Configuration}, {p.Add(text, NpgsqlDbType.Text)}) w)";
            return $"({q} IS NOT NULL AND d.search @@ {q} AND {p.Words(field)} @@ {q})";
        }));
    }
}

/// <summary>
/// Matches when every word of the value is within the edit distance of a word of the field, using
/// <c>levenshtein</c> from the <c>fuzzystrmatch</c> extension, which must be installed in the database. The edit
/// distance is the larger number of <c>Fuziness</c>, at most 2, or by word length as Elasticsearch's <c>AUTO</c>
/// (0 edits up to 2 characters, 1 up to 5, else 2). A transposition counts as two edits, as Levenshtein distance
/// does, where Elasticsearch counts it as one by default.
/// </summary>
internal sealed class FuzzySubQueryTranslator : PostgreSqlSubQueryTranslator<FuzzyQuery>
{
    private const int MaxEdits = 2;

    protected override Result<PostgreSqlFilter> Translate(FuzzyQuery query)
    {
        var field = RequireField(query.Field, nameof(FuzzyQuery));
        if (field.IsFailed)
        {
            return field.ToResult<PostgreSqlFilter>();
        }

        if (query.Value is null)
        {
            return Result.Fail<PostgreSqlFilter>($"{nameof(FuzzyQuery)} needs a Value.");
        }

        // A date or boolean is not text to match within a few edits.
        if (query.Value is not string && !QueryValueText.IsNumber(query.Value))
        {
            return Result.Fail<PostgreSqlFilter>(
                $"{nameof(FuzzyQuery)} value '{QueryValueText.From(query.Value)}' is a {query.Value.GetType().Name}; a fuzzy match needs a string or a number.");
        }

        var text = QueryValueText.From(query.Value);
        int? edits = query.Fuziness == null ? null : Math.Clamp(Math.Max(query.Fuziness.Item1, query.Fuziness.Item2), 0, MaxEdits);
        var prefixLength = Math.Max(query.PrefixLenght, 0);
        var name = field.Value;
        return Result.Ok<PostgreSqlFilter>(new PostgreSqlFilter.Condition($"{nameof(FuzzyQuery)} on {name}", p =>
        {
            var distance = edits is { } fixedEdits
                ? p.Add(fixedEdits, NpgsqlDbType.Integer)
                : "(CASE WHEN length(v) <= 2 THEN 0 WHEN length(v) <= 5 THEN 1 ELSE 2 END)";
            var prefix = p.Add(prefixLength, NpgsqlDbType.Integer);
            return $"coalesce((SELECT bool_and(EXISTS (SELECT 1 FROM unnest(tsvector_to_array({p.Words(name)})) t " +
                   $"WHERE left(t, {prefix}) = left(v, {prefix}) AND levenshtein(t, v) <= {distance})) " +
                   $"FROM unnest(tsvector_to_array(to_tsvector({p.Configuration}, {p.Add(text, NpgsqlDbType.Text)}))) v), false)";
        }));
    }
}
