using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bielu.BetterSearch.Meilisearch.Services;

/// <summary>
/// Writes a whole <see cref="double"/> or <see cref="float"/> with a fraction, <c>3.0</c> rather than <c>3</c>, so it
/// comes back as a <see cref="double"/> as it does from Elasticsearch and OpenSearch. Everything else is written and
/// read as <see cref="JsonSerializer"/> would.
/// </summary>
internal sealed class MeilisearchFieldsConverter : JsonConverter<IDictionary<string, IList<object>>>
{
    public override IDictionary<string, IList<object>>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<Dictionary<string, IList<object>>>(ref reader, options);

    public override void Write(Utf8JsonWriter writer, IDictionary<string, IList<object>> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (name, values) in value)
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
                    JsonSerializer.Serialize(writer, item, options);
                }
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    /// <summary>Whole numbers small enough to be written exactly without an exponent.</summary>
    private static double? WholeNumber(object? value) => value switch
    {
        double number when double.IsFinite(number) && Math.Abs(number) < 1e15 && Math.Floor(number) == number => number,
        float number when float.IsFinite(number) && Math.Abs(number) < 1e7 && MathF.Floor(number) == number => number,
        _ => null
    };
}
