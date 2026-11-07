using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.Meilisearch.Services.Queries;

/// <summary>Matches a word of the field starting with the value, ignoring case.</summary>
internal sealed class PrefixSubQueryTranslator : MeilisearchSubQueryTranslator<PrefixSubQuery>
{
    protected override Result<MeilisearchFilter> Translate(PrefixSubQuery query, IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators)
    {
        var field = MeilisearchFieldNames.Require(query.Field, nameof(PrefixSubQuery));
        if (field.IsFailed)
        {
            return field.ToResult<MeilisearchFilter>();
        }

        if (query.Value is not string { Length: > 0 } prefix)
        {
            return Result.Fail<MeilisearchFilter>($"{nameof(PrefixSubQuery)} needs a string Value.");
        }

        var value = MeilisearchFieldNames.IsStoredValue(field.Value) ? prefix : prefix.ToLowerInvariant();
        return Filter($"{MeilisearchFieldNames.In(MeilisearchFieldNames.Tokens, field.Value)} STARTS WITH {MeilisearchFieldNames.Quote(value)}");
    }
}

/// <summary>
/// Matches the words of the value in order, the last one as a prefix, as an Elasticsearch
/// <c>match_phrase_prefix</c> query does. The phrase can have at most <see cref="MeilisearchDocument.MaxPhraseTokens"/> words.
/// </summary>
internal sealed class PrefixPhraseSubQueryTranslator : MeilisearchSubQueryTranslator<PrefixPhraseSubQuery>
{
    protected override Result<MeilisearchFilter> Translate(PrefixPhraseSubQuery query, IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators)
    {
        var field = MeilisearchFieldNames.Require(query.Field, nameof(PrefixPhraseSubQuery));
        if (field.IsFailed)
        {
            return field.ToResult<MeilisearchFilter>();
        }

        var tokens = query.Value is string text ? MeilisearchDocument.Tokenize(text) : [];
        if (tokens.Count == 0)
        {
            return Result.Fail<MeilisearchFilter>($"{nameof(PrefixPhraseSubQuery)} needs a string Value with at least one word.");
        }

        if (tokens.Count > MeilisearchDocument.MaxPhraseTokens)
        {
            return Result.Fail<MeilisearchFilter>(
                $"{nameof(PrefixPhraseSubQuery)} has {tokens.Count} words; Meilisearch matches phrases of at most {MeilisearchDocument.MaxPhraseTokens}.");
        }

        var phrase = MeilisearchFieldNames.Quote(string.Join(' ', tokens));
        return Filter($"{MeilisearchFieldNames.In(MeilisearchFieldNames.Phrases, field.Value)} STARTS WITH {phrase}");
    }
}
