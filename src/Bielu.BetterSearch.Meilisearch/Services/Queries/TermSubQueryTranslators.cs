using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.Meilisearch.Services.Queries;

/// <summary>
/// A string matches one word of the field, ignoring case, as an Elasticsearch term query matches one
/// analyzed token; a <c>fields.</c> name matches the whole value instead. Numbers, booleans and dates
/// match the stored value.
/// </summary>
internal sealed class TermSubQueryTranslator : MeilisearchSubQueryTranslator<TermSubQuery>
{
    protected override Result<MeilisearchFilter> Translate(TermSubQuery query, IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators)
    {
        var field = MeilisearchFieldNames.Require(query.Field, nameof(TermSubQuery));
        if (field.IsFailed)
        {
            return field.ToResult<MeilisearchFilter>();
        }

        var term = TermValues.Equals(field.Value, query.Value, nameof(TermSubQuery));
        return term.IsFailed ? term.ToResult<MeilisearchFilter>() : Filter(term.Value);
    }
}

/// <summary>Matches any of the values, each as <see cref="TermSubQueryTranslator"/> does. No values match nothing.</summary>
internal sealed class TermsSubQueryTranslator : MeilisearchSubQueryTranslator<TermsSubQuery>
{
    protected override Result<MeilisearchFilter> Translate(TermsSubQuery query, IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators)
    {
        var field = MeilisearchFieldNames.Require(query.Field, nameof(TermsSubQuery));
        if (field.IsFailed)
        {
            return field.ToResult<MeilisearchFilter>();
        }

        if (query.Value is not { Count: > 0 })
        {
            return Filter(MeilisearchFieldNames.MatchNone);
        }

        var terms = new List<string>();
        foreach (var value in query.Value)
        {
            var term = TermValues.Equals(field.Value, value, nameof(TermsSubQuery));
            if (term.IsFailed)
            {
                return term.ToResult<MeilisearchFilter>();
            }

            terms.Add(term.Value);
        }

        return Filter(string.Join(" OR ", terms));
    }
}

internal static class TermValues
{
    public static Result<string> Equals(string field, object? value, string queryName)
    {
        switch (value)
        {
            case null:
                return Result.Fail<string>($"{queryName} needs a Value.");
            case string text:
                var term = MeilisearchFieldNames.IsStoredValue(field) ? text : text.ToLowerInvariant();
                return Result.Ok($"{MeilisearchFieldNames.In(MeilisearchFieldNames.Tokens, field)} = {MeilisearchFieldNames.Quote(term)}");
            case bool flag:
                return Result.Ok($"{MeilisearchFieldNames.In(MeilisearchFieldNames.Fields, field)} = {(flag ? "true" : "false")}");
        }

        if (MeilisearchDocument.ToUnixMilliseconds(value) is { } date)
        {
            return Result.Ok($"{MeilisearchFieldNames.In(MeilisearchFieldNames.Dates, field)} = {date}");
        }

        return MeilisearchFieldNames.Number(value) is { } number
            ? Result.Ok($"{MeilisearchFieldNames.In(MeilisearchFieldNames.Fields, field)} = {number}")
            : Result.Fail<string>($"{queryName} value of type {value.GetType().Name} is not supported by Meilisearch.");
    }
}
