using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Bielu.BetterSearch.Services;
using FluentResults;

namespace Bielu.BetterSearch.PostgreSql.Services;

/// <summary>
/// Converts a document's fields to the <c>jsonb</c> object a row stores and back. Each field is an array of its values.
/// A <see cref="double"/>, <see cref="float"/> or <see cref="decimal"/> is written with a fraction (<c>3.0</c>), which
/// <c>jsonb</c> keeps, so it comes back as a <see cref="double"/> rather than a <see cref="long"/>; integers are exact
/// at any size, as <c>jsonb</c> numbers are decimals. Dates are stored as <see cref="QueryValueText"/> writes them.
/// </summary>
internal static class PostgreSqlDocument
{
    /// <summary>The fields as a JSON object, or a failure naming the first value JSON can't hold.</summary>
    public static Result<string> FieldsJson(IDictionary<string, IList<object>>? fields)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (name, values) in StoredFields.WithDatesAsText(fields ?? new Dictionary<string, IList<object>>()))
            {
                writer.WritePropertyName(name);
                if (values == null)
                {
                    writer.WriteNullValue();
                    continue;
                }

                writer.WriteStartArray();
                foreach (var value in values)
                {
                    var written = Write(writer, value, name);
                    if (written.IsFailed)
                    {
                        return written.ToResult<string>();
                    }
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return Result.Ok(Encoding.UTF8.GetString(stream.ToArray()));
    }

    /// <summary>A JSON array holding <paramref name="value"/>, as a query compares it with stored values.</summary>
    public static Result<string> ValueJson(object value, string name)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            var written = Write(writer, value, name);
            if (written.IsFailed)
            {
                return written.ToResult<string>();
            }
        }

        return Result.Ok(Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static Result Write(Utf8JsonWriter writer, object? value, string name)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                return Result.Ok();
            case string text:
                writer.WriteStringValue(text);
                return Result.Ok();
            case bool flag:
                writer.WriteBooleanValue(flag);
                return Result.Ok();
            case JsonElement element:
                element.WriteTo(writer);
                return Result.Ok();
            case byte or sbyte or short or ushort or int or uint or long:
                writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                return Result.Ok();
            case ulong big:
                writer.WriteNumberValue(big);
                return Result.Ok();
            case float or double:
                var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (!double.IsFinite(number))
                {
                    return Result.Fail($"Field {name} holds {number.ToString(CultureInfo.InvariantCulture)}, which PostgreSQL jsonb can't store; use a finite number.");
                }

                writer.WriteRawValue(Fractional(number));
                return Result.Ok();
            case decimal exact:
                var digits = exact.ToString(CultureInfo.InvariantCulture);
                writer.WriteRawValue(digits.Contains('.', StringComparison.Ordinal) ? digits : digits + ".0");
                return Result.Ok();
            case IDictionary dictionary:
                writer.WriteStartObject();
                foreach (DictionaryEntry entry in dictionary)
                {
                    writer.WritePropertyName(Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty);
                    var nested = Write(writer, entry.Value, name);
                    if (nested.IsFailed)
                    {
                        return nested;
                    }
                }

                writer.WriteEndObject();
                return Result.Ok();
            default:
                writer.WriteStringValue(QueryValueText.From(value));
                return Result.Ok();
        }
    }

    /// <summary>
    /// The number as JSON with a fraction or an exponent, so it is read back as a <see cref="double"/>. A whole number
    /// is written in full with <c>.0</c>, as <c>jsonb</c> writes an exponent out in full and would drop the
    /// difference between <c>1E+15</c> and an integer.
    /// </summary>
    internal static string Fractional(double number)
    {
        if (Math.Floor(number) == number)
        {
            // Exact: a conversion to decimal would round to 15 significant digits.
            return new BigInteger(number).ToString(CultureInfo.InvariantCulture) + ".0";
        }

        return number.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>The stored fields, read back as values of the same types they were indexed with.</summary>
    public static IDictionary<string, IList<object>> ReadFields(string json)
    {
        using var document = JsonDocument.Parse(json);
        var fields = new Dictionary<string, IList<object>>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            fields[property.Name] = property.Value.ValueKind == JsonValueKind.Array
                ? property.Value.EnumerateArray().Select(element => StoredFields.ToClrValue(element.Clone())).OfType<object>().ToList()
                : [];
        }

        return fields;
    }
}
