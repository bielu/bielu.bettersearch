using System.Globalization;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.Meilisearch.Services.Queries;

/// <summary>
/// A string matches one word of the field, ignoring case, as an Elasticsearch term query matches one
/// analyzed token; a <c>fields.</c> name matches the whole value instead. A string that reads as a number or
/// boolean also matches that stored value, as Elasticsearch coerces it for a numeric or boolean field. Numbers,
/// booleans and dates match the stored value. Meilisearch compares strings ignoring case, so <c>fields.</c> does too.
/// </summary>
internal sealed class TermSubQueryTranslator : MeilisearchSubQueryTranslator<TermSubQuery>
{
    protected override Result<MeilisearchFilter> Translate(TermSubQuery query, IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators) =>
        TermValues.Filter(query.Field, [query.Value], nameof(TermSubQuery));
}

/// <summary>Matches any of the values, each as <see cref="TermSubQueryTranslator"/> does. No values match nothing.</summary>
internal sealed class TermsSubQueryTranslator : MeilisearchSubQueryTranslator<TermsSubQuery>
{
    protected override Result<MeilisearchFilter> Translate(TermsSubQuery query, IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators) =>
        TermValues.Filter(query.Field, query.Value ?? [], nameof(TermsSubQuery));
}

internal static class TermValues
{
    /// <summary>One <c>IN</c> per attribute the values compare, or <c>=</c> for a single value, joined by <c>OR</c>.</summary>
    public static Result<MeilisearchFilter> Filter(string? queryField, IEnumerable<object?> values, string queryName)
    {
        var field = MeilisearchFieldNames.Require(queryField, queryName);
        if (field.IsFailed)
        {
            return field.ToResult<MeilisearchFilter>();
        }

        var byAttribute = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            var matches = Matches(field.Value, value, queryName);
            if (matches.IsFailed)
            {
                return matches.ToResult<MeilisearchFilter>();
            }

            foreach (var (attribute, literal) in matches.Value)
            {
                if (!byAttribute.TryGetValue(attribute, out var literals))
                {
                    literals = [];
                    byAttribute[attribute] = literals;
                }

                if (!literals.Contains(literal, StringComparer.Ordinal))
                {
                    literals.Add(literal);
                }
            }
        }

        if (byAttribute.Count == 0)
        {
            return Result.Ok(new MeilisearchFilter(MeilisearchFieldNames.MatchNone));
        }

        return Result.Ok(new MeilisearchFilter(string.Join(" OR ", byAttribute.Select(pair => pair.Value.Count == 1
            ? $"{pair.Key} = {pair.Value[0]}"
            : $"{pair.Key} IN [{string.Join(", ", pair.Value)}]"))));
    }

    /// <summary>The attributes and literals one value matches, any of which is a match.</summary>
    private static Result<List<(string Attribute, string Literal)>> Matches(string field, object? value, string queryName)
    {
        switch (value)
        {
            case null:
                return Result.Fail<List<(string, string)>>($"{queryName} needs a Value.");
            case string text:
                var storedValue = MeilisearchFieldNames.IsStoredValue(field);
                List<(string, string)> matches =
                    [(MeilisearchFieldNames.In(MeilisearchFieldNames.Tokens, field), MeilisearchFieldNames.Quote(storedValue ? text : text.ToLowerInvariant()))];
                // A stored value already compares the text with numbers and booleans as given.
                if (!storedValue && Coerce(text) is { } coerced)
                {
                    matches.Add((MeilisearchFieldNames.In(MeilisearchFieldNames.Fields, field), coerced));
                }

                return Result.Ok(matches);
            case bool flag:
                return Result.Ok<List<(string, string)>>([(MeilisearchFieldNames.In(MeilisearchFieldNames.Fields, field), flag ? "true" : "false")]);
        }

        if (MeilisearchDocument.ToUnixMilliseconds(value) is { } date)
        {
            return Result.Ok<List<(string, string)>>([(MeilisearchFieldNames.In(MeilisearchFieldNames.Dates, field), date.ToString(CultureInfo.InvariantCulture))]);
        }

        return MeilisearchFieldNames.Number(value) is { } number
            ? Result.Ok<List<(string, string)>>([(MeilisearchFieldNames.In(MeilisearchFieldNames.Fields, field), number)])
            : Result.Fail<List<(string, string)>>($"{queryName} value of type {value.GetType().Name} is not supported by Meilisearch.");
    }

    /// <summary>The number or boolean <paramref name="text"/> reads as, as a filter literal.</summary>
    private static string? Coerce(string text)
    {
        if (bool.TryParse(text, out var flag))
        {
            return flag ? "true" : "false";
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
            ? MeilisearchFieldNames.Number(number)
            : null;
    }
}
