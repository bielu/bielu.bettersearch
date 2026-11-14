using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Meilisearch.Configuration;
using FluentResults;
using Microsoft.Extensions.Options;

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
internal sealed class PrefixPhraseSubQueryTranslator(IOptionsMonitor<MeilisearchOptions> optionsMonitor)
    : MeilisearchSubQueryTranslator<PrefixPhraseSubQuery>
{
    protected override Result<MeilisearchFilter> Translate(PrefixPhraseSubQuery query, IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators)
    {
        var field = MeilisearchFieldNames.Require(query.Field, nameof(PrefixPhraseSubQuery));
        if (field.IsFailed)
        {
            return field.ToResult<MeilisearchFilter>();
        }

        var options = optionsMonitor.Current();
        if (options.IsFailed)
        {
            return options.ToResult<MeilisearchFilter>();
        }

        // A fields. name compares the stored value, so it needs no phrases.
        if (!MeilisearchFieldNames.IsStoredValue(field.Value) && options.Value.PhraseFields is { } phraseFields
            && !phraseFields.Contains(field.Value))
        {
            return Result.Fail<MeilisearchFilter>(
                $"{nameof(PrefixPhraseSubQuery)} field '{field.Value}' is not in MeilisearchOptions.PhraseFields, so its phrases are not stored.");
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
