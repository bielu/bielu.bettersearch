using Bielu.BetterSearch.Services;
using FluentResults;
using Lifti.Querying.QueryParts;
using Lifti.Tokenization;

namespace Bielu.BetterSearch.Lifti.Services.Queries;

/// <summary>
/// Splits a query value into words with the tokenizer the index uses, so a value holding punctuation or
/// spaces (<c>3.5</c>, <c>11/08/2026 00:00:00</c>, <c>jane doe</c>) or accents (<c>żółw</c>) matches what was
/// indexed. A value of several words matches them next to each other, in order.
/// </summary>
internal static class LiftiQueryWords
{
    /// <summary>
    /// Put between the values of a multi-valued field, so the words of one value are never next to the words of
    /// another: <c>["3", "5"]</c> must not match <c>3.5</c>. The tokenizer keeps the private-use character as a
    /// word of its own, which only a query value holding that character can match.
    /// </summary>
    internal const string ValueSeparator = " \uE000 ";

    /// <summary>The value's words, normalized as the index stores them, in the order they appear.</summary>
    public static IReadOnlyList<string> Split(object? value) => Tokenize(value).Words;

    /// <summary>
    /// Matches the value's words next to each other, each through <paramref name="word"/>, and the last through
    /// <paramref name="lastWord"/> when given. Fails for a value with no words, which could never match.
    /// </summary>
    public static Result<IQueryPart> Match(object? value, string queryType, Func<string, IQueryPart> word,
        Func<string, IQueryPart>? lastWord = null)
    {
        var words = Split(value);
        if (words.Count == 0)
        {
            return NoWords(value, queryType);
        }

        var parts = words
            .Select((text, position) => position == words.Count - 1 && lastWord != null ? lastWord(text) : word(text))
            .ToList();
        return Result.Ok(Adjacent(parts));
    }

    /// <summary>
    /// Matches the value as a prefix: the words before the last whole, and the last as a prefix. When the value
    /// ends with characters the tokenizer drops (<c>3.</c>), its last word is complete, so it matches whole and
    /// must be followed by another word: <c>3.</c> matches <c>3.5</c>, not <c>35</c> or <c>3</c>.
    /// </summary>
    public static Result<IQueryPart> MatchPrefix(object? value, string queryType)
    {
        var (words, endsWithWord) = Tokenize(value);
        if (words.Count == 0)
        {
            return NoWords(value, queryType);
        }

        var parts = words.Select(IQueryPart (text) => new ExactWordQueryPart(text)).ToList();
        if (endsWithWord)
        {
            parts[^1] = new WildcardQueryPart(WildcardQueryFragment.CreateText(words[^1]), WildcardQueryFragment.MultiCharacter);
        }
        else
        {
            parts.Add(new WildcardQueryPart(WildcardQueryFragment.MultiCharacter));
        }

        return Result.Ok(Adjacent(parts));
    }

    private static IQueryPart Adjacent(List<IQueryPart> parts) =>
        parts.Count == 1 ? parts[0] : new AdjacentWordsQueryOperator(parts);

    private static Result<IQueryPart> NoWords(object? value, string queryType) =>
        Result.Fail<IQueryPart>(
            $"Lifti cannot match the {queryType} value '{QueryValueText.From(value)}': it holds no words to search for.");

    /// <summary>The value's words in order, and whether the value ends with a word rather than with dropped characters.</summary>
    private static (IReadOnlyList<string> Words, bool EndsWithWord) Tokenize(object? value)
    {
        var text = QueryValueText.From(value);
        var words = IndexTokenizer.Default.Process(text.AsSpan())
            .SelectMany(token => token.Locations.Select(location => (Location: location, token.Value)))
            .OrderBy(word => word.Location.TokenIndex)
            .ToList();
        var endsWithWord = words.Count > 0 && words[^1].Location.Start + words[^1].Location.Length == text.Length;
        return (words.Select(word => word.Value).ToList(), endsWithWord);
    }
}
