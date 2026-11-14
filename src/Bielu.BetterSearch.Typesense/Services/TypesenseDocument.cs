using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bielu.BetterSearch.Services;
using FluentResults;

namespace Bielu.BetterSearch.Typesense.Services;

/// <summary>
/// How a <see cref="SearchDocument"/> is stored. <c>source</c> keeps the fields as given, as JSON text that Typesense
/// does not index, so results get back exactly what was indexed (each date as ISO 8601 text in UTC, as
/// <see cref="QueryValueText"/> writes it). The other fields are the typed copies the query translators filter and sort
/// on, named by <see cref="TypesenseFieldNames"/>.
/// </summary>
internal static partial class TypesenseDocument
{
    /// <summary>The longest run of words kept in <c>p_</c>, so a long text stays a bounded size.</summary>
    public const int MaxPhraseTokens = 16;

    public static Result<JsonObject> From(SearchDocument document, string? geoField)
    {
        var fields = document.Fields ?? new Dictionary<string, IList<object>>();
        var stored = new JsonObject
        {
            ["id"] = document.Id,
            [TypesenseFieldNames.Type] = document.Type,
            [TypesenseFieldNames.Index] = document.Index,
        };

        foreach (var (name, values) in fields)
        {
            if (!TypesenseFieldNames.IsValidName(name))
            {
                return Result.Fail<JsonObject>(
                    $"Document {document.Id} field '{name}' can only contain letters, digits and '_' for Typesense.");
            }

            var list = values ?? [];
            if (name == geoField && list.Count > 0)
            {
                if (list.Count > 1 || ToGeoPoint(list[0]) is not { } point)
                {
                    return Result.Fail<JsonObject>(
                        $"Document {document.Id} field {geoField} must hold one point, a \"lat,lon\" string or a dictionary with lat and lon, for Typesense.");
                }

                stored[TypesenseFieldNames.Geo + name] = new JsonArray(point.Lat, point.Lng);
            }

            foreach (var value in list)
            {
                switch (value)
                {
                    case string text:
                        var tokens = Tokenize(text);
                        Add(stored, TypesenseFieldNames.Strings + name, text);
                        foreach (var token in tokens)
                        {
                            Add(stored, TypesenseFieldNames.Tokens + name, token);
                        }

                        for (var start = 0; start < tokens.Count; start++)
                        {
                            Add(stored, TypesenseFieldNames.Phrases + name, string.Join(' ', tokens.Skip(start).Take(MaxPhraseTokens)));
                        }

                        break;
                    case bool flag:
                        Add(stored, TypesenseFieldNames.Booleans + name, flag);
                        break;
                    case byte or sbyte or short or ushort or int or uint or long:
                        Add(stored, TypesenseFieldNames.Integers + name, Convert.ToInt64(value, CultureInfo.InvariantCulture));
                        break;
                    case ulong big when big <= long.MaxValue:
                        Add(stored, TypesenseFieldNames.Integers + name, (long)big);
                        break;
                    // Typesense would wrap a ulong above long.MaxValue into a negative int64, so it is stored as a float.
                    case ulong or decimal or float or double:
                        var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                        if (!double.IsFinite(number))
                        {
                            return Result.Fail<JsonObject>(
                                $"Document {document.Id} field {name} holds {QueryValueText.From(value)}, which is not a finite number Typesense can store.");
                        }

                        Add(stored, TypesenseFieldNames.Numbers + name, number);
                        break;
                    default:
                        if (ToUnixMilliseconds(value) is { } date)
                        {
                            Add(stored, TypesenseFieldNames.Dates + name, date);
                        }

                        break;
                }
            }

            if (list.Count == 1)
            {
                switch (list[0])
                {
                    case string text:
                        stored[TypesenseFieldNames.SortString + name] = text;
                        break;
                    case byte or sbyte or short or ushort or int or uint or long:
                        stored[TypesenseFieldNames.SortInteger + name] = Convert.ToInt64(list[0], CultureInfo.InvariantCulture);
                        break;
                    case ulong big when big <= long.MaxValue:
                        stored[TypesenseFieldNames.SortInteger + name] = (long)big;
                        break;
                    case DateTime or DateTimeOffset or DateOnly:
                        stored[TypesenseFieldNames.SortInteger + name] = ToUnixMilliseconds(list[0]);
                        break;
                    case ulong or decimal or float or double:
                        var number = Convert.ToDouble(list[0], CultureInfo.InvariantCulture);
                        if (double.IsFinite(number))
                        {
                            stored[TypesenseFieldNames.SortNumber + name] = number;
                        }

                        break;
                }
            }
        }

        // Written after the values are checked, so a value that can't be stored fails the Result rather than the serializer.
        try
        {
            stored[TypesenseFieldNames.Source] = Source(StoredFields.WithDatesAsText(fields));
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            return Result.Fail<JsonObject>(new Error($"Document {document.Id} fields can't be written as JSON for Typesense: {e.Message}").CausedBy(e));
        }

        return Result.Ok(stored);
    }

    /// <summary>
    /// The document as Typesense stores it, every typed copy included; <c>null</c> when Typesense answers that the
    /// document or the collection is not there. A <c>.</c> or <c>..</c> id would be resolved away in a path, so such
    /// a document is read by an export filtered on its id instead.
    /// </summary>
    public static async Task<Result<JsonObject?>> GetAsync(TypesenseHttpClientWrapper client, string index, string id, CancellationToken cancellationToken)
    {
        var collection = $"collections/{TypesenseHttpClientWrapper.Escape(index)}/documents";
        var path = id is "." or ".."
            ? $"{collection}/export?filter_by={Uri.EscapeDataString($"id:[`{id}`]")}"
            : $"{collection}/{TypesenseHttpClientWrapper.Escape(id)}";
        var response = await client.SendAsync(HttpMethod.Get, path, null, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Result.Ok<JsonObject?>(null);
        }

        if (!response.IsSuccess)
        {
            return Result.Fail<JsonObject?>(response.Describe());
        }

        // An export answers one document per line, and no line when no document has the id.
        var line = response.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return Result.Ok(line is null ? null : JsonNode.Parse(line) as JsonObject);
    }

    /// <summary>The fields a hit's <c>source</c> holds.</summary>
    public static IDictionary<string, IList<object>>? ReadSource(string? source) =>
        string.IsNullOrEmpty(source) ? null : JsonSerializer.Deserialize<Dictionary<string, IList<object>>>(source);

    /// <summary>
    /// The fields as JSON text. A whole <see cref="double"/> or <see cref="float"/> keeps its fraction, <c>3.0</c>
    /// rather than <c>3</c>, so it comes back as a <see cref="double"/> as it does from Elasticsearch and OpenSearch.
    /// </summary>
    private static string Source(IDictionary<string, IList<object>> fields)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (name, values) in fields)
            {
                writer.WritePropertyName(name);
                if (values is null)
                {
                    writer.WriteNullValue();
                    continue;
                }

                writer.WriteStartArray();
                foreach (var item in values)
                {
                    if (WholeNumber(item) is { } number)
                    {
                        writer.WriteRawValue(number.ToString("F1", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        JsonSerializer.Serialize(writer, item);
                    }
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Whole numbers small enough to be written exactly without an exponent.</summary>
    private static double? WholeNumber(object? value) => value switch
    {
        double number when double.IsFinite(number) && Math.Abs(number) < 1e15 && Math.Floor(number) == number => number,
        float number when float.IsFinite(number) && Math.Abs(number) < 1e7 && MathF.Floor(number) == number => number,
        _ => null
    };

    /// <summary>A <c>"lat,lon"</c> string or a dictionary with <c>lat</c> and <c>lon</c> or <c>lng</c>, within range.</summary>
    public static (double Lat, double Lng)? ToGeoPoint(object? value)
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

        return lat is >= -90 and <= 90 && lng is >= -180 and <= 180 ? (lat.Value, lng.Value) : null;
    }

    private static double? ParseCoordinate(object? value) => value switch
    {
        string text => double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null,
        null or bool => null,
        IConvertible number when QueryValueText.IsNumber(number) => Convert.ToDouble(number, CultureInfo.InvariantCulture),
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
        DateOnly day => new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds(),
        _ => null
    };

    private static void Add(JsonObject document, string field, JsonNode? value)
    {
        if (document[field] is not JsonArray array)
        {
            array = [];
            document[field] = array;
        }

        array.Add(value);
    }

    [GeneratedRegex(@"[^\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonWordCharacters();
}
