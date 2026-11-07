using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Bielu.BetterSearch.Meilisearch.Services;

/// <summary>
/// How a <see cref="SearchDocument"/> is stored. <c>fields</c> keeps the values as given, for results. The
/// other maps are the copies the query translators filter on, as Meilisearch filters compare whole values:
/// <c>tokens</c> holds each word of a text value lower-cased, <c>phrases</c> the runs of words a phrase
/// prefix can start at, and <c>dates</c> each date as Unix milliseconds.
/// </summary>
internal sealed partial class MeilisearchDocument
{
    /// <summary>The longest run of words kept in <c>phrases</c>, so a long text stays a bounded size.</summary>
    public const int MaxPhraseTokens = 16;

    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("index")] public string? Index { get; set; }
    [JsonPropertyName(MeilisearchFieldNames.Fields)] public IDictionary<string, IList<object>> Fields { get; set; } = new Dictionary<string, IList<object>>();
    [JsonPropertyName(MeilisearchFieldNames.Tokens)] public Dictionary<string, List<string>> Tokens { get; set; } = [];
    [JsonPropertyName(MeilisearchFieldNames.Phrases)] public Dictionary<string, List<string>> Phrases { get; set; } = [];
    [JsonPropertyName(MeilisearchFieldNames.Dates)] public Dictionary<string, List<long>> Dates { get; set; } = [];

    /// <summary>The hit's ranking score, returned on search hits only.</summary>
    [JsonPropertyName("_rankingScore")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? RankingScore { get; set; }

    public static MeilisearchDocument From(SearchDocument document)
    {
        var stored = new MeilisearchDocument { Id = document.Id, Type = document.Type, Index = document.Index, Fields = document.Fields };
        foreach (var (name, values) in document.Fields)
        {
            foreach (var value in values)
            {
                if (value is string text)
                {
                    var tokens = Tokenize(text);
                    Add(stored.Tokens, name, tokens);
                    Add(stored.Phrases, name, tokens.Select((_, start) =>
                        string.Join(' ', tokens.Skip(start).Take(MaxPhraseTokens))));
                }
                else if (ToUnixMilliseconds(value) is { } date)
                {
                    Add(stored.Dates, name, [date]);
                }
            }
        }

        return stored;
    }

    /// <summary>Splits text into lower-cased words, breaking on anything that is not a letter or digit.</summary>
    public static List<string> Tokenize(string text) =>
        NonWordCharacters().Split(text.ToLowerInvariant()).Where(token => token.Length > 0).ToList();

    /// <summary>A date without a time zone is taken as UTC, as Elasticsearch does.</summary>
    public static long? ToUnixMilliseconds(object? value) => value switch
    {
        DateTimeOffset offset => offset.ToUnixTimeMilliseconds(),
        DateTime { Kind: DateTimeKind.Unspecified } date => new DateTimeOffset(date, TimeSpan.Zero).ToUnixTimeMilliseconds(),
        DateTime date => new DateTimeOffset(date.ToUniversalTime()).ToUnixTimeMilliseconds(),
        _ => null
    };

    private static void Add<T>(Dictionary<string, List<T>> map, string name, IEnumerable<T> values)
    {
        if (!map.TryGetValue(name, out var list))
        {
            list = [];
            map[name] = list;
        }

        list.AddRange(values);
    }

    [GeneratedRegex(@"[^\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonWordCharacters();
}
