using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using FluentResults;

namespace Bielu.BetterSearch.Meilisearch.Services;

/// <summary>
/// How a <see cref="SearchDocument"/> is stored. <c>fields</c> keeps the values as given, for results. The
/// other maps are the copies the query translators filter on, as Meilisearch filters compare whole values:
/// <c>tokens</c> holds each word of a text value lower-cased, <c>phrases</c> the runs of words a phrase
/// prefix can start at, and <c>dates</c> each date as Unix milliseconds. <c>_geo</c> holds the point of the field named
/// by <c>MeilisearchOptions.GeoField</c>, the only attribute Meilisearch filters by distance on.
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

    [JsonPropertyName(MeilisearchFieldNames.Geo)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeoPoint? Geo { get; set; }

    /// <summary>The hit's ranking score, returned on search hits only.</summary>
    [JsonPropertyName("_rankingScore")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? RankingScore { get; set; }

    /// <summary>The stored form of <paramref name="document"/>, with phrases for <paramref name="phraseFields"/>, or every field when <c>null</c>.</summary>
    public static Result<MeilisearchDocument> From(SearchDocument document, string? geoField, ICollection<string>? phraseFields = null)
    {
        var stored = new MeilisearchDocument { Id = document.Id, Type = document.Type, Index = document.Index, Fields = document.Fields };
        if (geoField != null && document.Fields.TryGetValue(geoField, out var locations) && locations is { Count: > 0 })
        {
            if (locations.Count > 1 || ToGeoPoint(locations[0]) is not { } point)
            {
                return Result.Fail<MeilisearchDocument>(
                    $"Document {document.Id} field {geoField} must hold one point, a \"lat,lon\" string or a dictionary with lat and lon, for Meilisearch.");
            }

            stored.Geo = point;
        }

        foreach (var (name, values) in document.Fields)
        {
            foreach (var value in values ?? [])
            {
                if (value is string text)
                {
                    var tokens = Tokenize(text);
                    Add(stored.Tokens, name, tokens);
                    if (phraseFields == null || phraseFields.Contains(name))
                    {
                        Add(stored.Phrases, name, tokens.Select((_, start) =>
                            string.Join(' ', tokens.Skip(start).Take(MaxPhraseTokens))));
                    }
                }
                else if (ToUnixMilliseconds(value) is { } date)
                {
                    Add(stored.Dates, name, [date]);
                }
            }
        }

        return Result.Ok(stored);
    }

    /// <summary>A <c>"lat,lon"</c> string or a dictionary with <c>lat</c> and <c>lon</c> or <c>lng</c>, within range.</summary>
    public static GeoPoint? ToGeoPoint(object? value)
    {
        double? lat = null, lng = null;
        switch (value)
        {
            case string text when text.Split(',') is [var first, var second]:
                lat = ParseCoordinate(first);
                lng = ParseCoordinate(second);
                break;
            case IDictionary<string, object> point:
                lat = ParseCoordinate(point.TryGetValue("lat", out var latValue) ? latValue : null);
                lng = ParseCoordinate(point.TryGetValue("lon", out var lonValue) ? lonValue
                    : point.TryGetValue("lng", out var lngValue) ? lngValue : null);
                break;
        }

        return lat is >= -90 and <= 90 && lng is >= -180 and <= 180 ? new GeoPoint(lat.Value, lng.Value) : null;
    }

    private static double? ParseCoordinate(object? value) => value switch
    {
        string text => double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null,
        null or bool => null,
        IConvertible number when MeilisearchFieldNames.Number(number) != null => Convert.ToDouble(number, CultureInfo.InvariantCulture),
        _ => null
    };

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

    public sealed record GeoPoint(
        [property: JsonPropertyName("lat")] double Lat,
        [property: JsonPropertyName("lng")] double Lng);

    [GeneratedRegex(@"[^\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonWordCharacters();
}
