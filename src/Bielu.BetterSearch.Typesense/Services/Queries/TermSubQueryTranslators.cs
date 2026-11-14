using System.Globalization;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using N = Bielu.BetterSearch.Typesense.Services.TypesenseFieldNames;

namespace Bielu.BetterSearch.Typesense.Services.Queries;

/// <summary>
/// A string matches one word of the field, ignoring case, as an Elasticsearch term query matches one analyzed token; a
/// <c>fields.</c> name matches the whole value instead, also ignoring case, as Typesense compares strings. A string that
/// reads as a number or boolean also matches that stored value, as Elasticsearch coerces it for a numeric or boolean
/// field. Numbers, booleans and dates match the stored value.
/// </summary>
internal sealed class TermSubQueryTranslator : TypesenseSubQueryTranslator<TermSubQuery>
{
    protected override Result<TypesenseFilter> Translate(TermSubQuery query, IEnumerable<ISubQueryTranslator<TypesenseFilter>> subQueryTranslators) =>
        TermValues.Filter(query.Field, [query.Value], nameof(TermSubQuery));
}

/// <summary>Matches any of the values, each as <see cref="TermSubQueryTranslator"/> does. No values match nothing.</summary>
internal sealed class TermsSubQueryTranslator : TypesenseSubQueryTranslator<TermsSubQuery>
{
    protected override Result<TypesenseFilter> Translate(TermsSubQuery query, IEnumerable<ISubQueryTranslator<TypesenseFilter>> subQueryTranslators) =>
        TermValues.Filter(query.Field, query.Value ?? [], nameof(TermsSubQuery));
}

internal static class TermValues
{
    /// <summary>One <c>:=[...]</c> per Typesense field the values compare, joined by <c>||</c>; its negation is <c>:!=[...]</c>.</summary>
    public static Result<TypesenseFilter> Filter(string? queryField, IEnumerable<object?> values, string queryName)
    {
        var field = N.Require(queryField, queryName);
        if (field.IsFailed)
        {
            return field.ToResult<TypesenseFilter>();
        }

        var byField = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            var matches = Matches(field.Value, value, queryName);
            if (matches.IsFailed)
            {
                return matches.ToResult<TypesenseFilter>();
            }

            foreach (var (typesenseField, literal) in matches.Value)
            {
                if (!byField.TryGetValue(typesenseField, out var literals))
                {
                    literals = [];
                    byField[typesenseField] = literals;
                }

                if (!literals.Contains(literal, StringComparer.Ordinal))
                {
                    literals.Add(literal);
                }
            }
        }

        var comparisons = byField.Select(pair => (TypesenseFilter)new TypesenseFilter.Comparison(pair.Key,
            $"{pair.Key}:=[{string.Join(", ", pair.Value)}]", $"{pair.Key}:!=[{string.Join(", ", pair.Value)}]")).ToList();
        return Result.Ok(comparisons.Count switch
        {
            0 => TypesenseFilter.MatchNone,
            1 => comparisons[0],
            _ => new TypesenseFilter.Or(comparisons)
        });
    }

    /// <summary>The Typesense fields and literals one value matches, any of which is a match.</summary>
    private static Result<List<(string Field, string Literal)>> Matches(string field, object? value, string queryName)
    {
        var name = N.Name(field);
        switch (value)
        {
            case null:
                return Result.Fail<List<(string, string)>>($"{queryName} needs a Value.");
            case string text:
                var storedValue = N.IsStoredValue(field);
                var quoted = N.Quote(storedValue ? text : text.ToLowerInvariant(), queryName);
                if (quoted.IsFailed)
                {
                    return quoted.ToResult<List<(string, string)>>();
                }

                List<(string, string)> matches = [((storedValue ? N.Strings : N.Tokens) + name, quoted.Value)];
                if (bool.TryParse(text, out var parsedFlag))
                {
                    matches.Add((N.Booleans + name, parsedFlag ? "true" : "false"));
                }
                else if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
                {
                    matches.AddRange(Numbers(name, long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer) ? integer : number));
                }

                return Result.Ok(matches);
            case bool flag:
                return Result.Ok<List<(string, string)>>([(N.Booleans + name, flag ? "true" : "false")]);
        }

        if (TypesenseDocument.ToUnixMilliseconds(value) is { } date)
        {
            return Result.Ok<List<(string, string)>>([(N.Dates + name, N.Number(date))]);
        }

        var finite = N.RequireFinite(value, queryName);
        if (finite.IsFailed)
        {
            return finite.ToResult<List<(string, string)>>();
        }

        return value switch
        {
            byte or sbyte or short or ushort or int or uint or long => Result.Ok(Numbers(name, Convert.ToInt64(value, CultureInfo.InvariantCulture))),
            ulong big => Result.Ok(big <= long.MaxValue ? Numbers(name, (long)big) : Numbers(name, (double)big)),
            decimal or float or double => Result.Ok(Numbers(name, Convert.ToDouble(value, CultureInfo.InvariantCulture))),
            _ => Result.Fail<List<(string, string)>>($"{queryName} value of type {value.GetType().Name} is not supported by Typesense.")
        };
    }

    /// <summary>An integer matches both the integers and the other numbers the provider stores.</summary>
    private static List<(string, string)> Numbers(string name, long value) =>
        [(N.Integers + name, N.Number(value)), (N.Numbers + name, N.Number((double)value))];

    /// <summary>A number with a fraction only matches the numbers stored as floats; a whole one matches integers too.</summary>
    private static List<(string, string)> Numbers(string name, double value) =>
        Math.Floor(value) == value && value is >= long.MinValue and < long.MaxValue
            ? Numbers(name, (long)value)
            : [(N.Numbers + name, N.Number(value))];
}
