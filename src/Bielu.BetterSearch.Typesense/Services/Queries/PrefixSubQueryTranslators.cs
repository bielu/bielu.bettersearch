using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using N = Bielu.BetterSearch.Typesense.Services.TypesenseFieldNames;

namespace Bielu.BetterSearch.Typesense.Services.Queries;

/// <summary>Matches a word of the field starting with the value, ignoring case; a <c>fields.</c> name matches a whole value starting with it.</summary>
internal sealed class PrefixSubQueryTranslator : TypesenseSubQueryTranslator<PrefixSubQuery>
{
    protected override Result<TypesenseFilter> Translate(PrefixSubQuery query, IEnumerable<ISubQueryTranslator<TypesenseFilter>> subQueryTranslators)
    {
        var field = N.Require(query.Field, nameof(PrefixSubQuery));
        if (field.IsFailed)
        {
            return field.ToResult<TypesenseFilter>();
        }

        if (query.Value is not string { Length: > 0 } prefix)
        {
            return Result.Fail<TypesenseFilter>($"{nameof(PrefixSubQuery)} needs a string Value.");
        }

        var stored = N.IsStoredValue(field.Value);
        return PrefixFilter.Create((stored ? N.Strings : N.Tokens) + N.Name(field.Value), stored ? prefix : prefix.ToLowerInvariant(), nameof(PrefixSubQuery));
    }
}

/// <summary>
/// Matches the words of the value in order, the last one as a prefix, as an Elasticsearch
/// <c>match_phrase_prefix</c> query does. The phrase can have at most <see cref="TypesenseDocument.MaxPhraseTokens"/> words.
/// </summary>
internal sealed class PrefixPhraseSubQueryTranslator : TypesenseSubQueryTranslator<PrefixPhraseSubQuery>
{
    protected override Result<TypesenseFilter> Translate(PrefixPhraseSubQuery query, IEnumerable<ISubQueryTranslator<TypesenseFilter>> subQueryTranslators)
    {
        var field = N.Require(query.Field, nameof(PrefixPhraseSubQuery));
        if (field.IsFailed)
        {
            return field.ToResult<TypesenseFilter>();
        }

        if (N.IsStoredValue(field.Value))
        {
            return query.Value is string { Length: > 0 } whole
                ? PrefixFilter.Create(N.Strings + N.Name(field.Value), whole, nameof(PrefixPhraseSubQuery))
                : Result.Fail<TypesenseFilter>($"{nameof(PrefixPhraseSubQuery)} needs a string Value.");
        }

        var tokens = query.Value is string text ? TypesenseDocument.Tokenize(text) : [];
        if (tokens.Count == 0)
        {
            return Result.Fail<TypesenseFilter>($"{nameof(PrefixPhraseSubQuery)} needs a string Value with at least one word.");
        }

        if (tokens.Count > TypesenseDocument.MaxPhraseTokens)
        {
            return Result.Fail<TypesenseFilter>(
                $"{nameof(PrefixPhraseSubQuery)} has {tokens.Count} words; Typesense matches phrases of at most {TypesenseDocument.MaxPhraseTokens}.");
        }

        return PrefixFilter.Create(N.Phrases + N.Name(field.Value), string.Join(' ', tokens), nameof(PrefixPhraseSubQuery));
    }
}

internal static class PrefixFilter
{
    private const string NotNegatable = "Typesense filters cannot negate a prefix, so a prefix can't be a MUSTNOT clause.";

    public static Result<TypesenseFilter> Create(string field, string prefix, string queryName)
    {
        var quoted = N.Quote(prefix, queryName);
        return quoted.IsFailed
            ? quoted.ToResult<TypesenseFilter>()
            : Result.Ok<TypesenseFilter>(new TypesenseFilter.Comparison(field, $"{field}:={quoted.Value}*", null, NotNegatable));
    }
}
