using System.Text.Json;
using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// Maps a stored document, from a hit or a get, to a <see cref="SearchDocument"/> the same way on every provider: a field stored as
/// <c>null</c> becomes an empty list, <c>null</c> values are left out and <see cref="JsonElement"/> values become
/// plain values.
/// </summary>
internal static class StoredFields
{
    /// <summary>
    /// <paramref name="fields"/> as they are sent to the engine: each date as <see cref="QueryValueText"/> writes it,
    /// so the stored text is the text a query sends, and any other value as given.
    /// </summary>
    public static IDictionary<string, IList<object>> WithDatesAsText(IDictionary<string, IList<object>> fields) =>
        fields.ToDictionary(
            field => field.Key,
            field => field.Value is { } values && values.Any(QueryValueText.IsDate)
                ? values.Select(value => QueryValueText.IsDate(value) ? QueryValueText.From(value) : value).ToList()
                : field.Value);

    /// <summary>A copy of <paramref name="document"/> with its fields <see cref="WithDatesAsText">as they are sent to the engine</see>.</summary>
    public static SearchDocument WithDatesAsText(SearchDocument document) => new()
    {
        Id = document.Id,
        Type = document.Type,
        Index = document.Index,
        Fields = WithDatesAsText(document.Fields)
    };

    /// <summary>
    /// The document a hit or a get read: <paramref name="id"/> unchanged, a <c>null</c> type as an empty one, the
    /// caller's <paramref name="index"/> and the stored <paramref name="fields"/> as <see cref="ToFields"/> maps them.
    /// </summary>
    public static SearchDocument ToDocument(string id, string? type, string index, IDictionary<string, IList<object>>? fields) => new()
    {
        Id = id,
        Type = type ?? string.Empty,
        Index = index,
        Fields = ToFields(fields)
    };

    public static IDictionary<string, IList<object>> ToFields(IDictionary<string, IList<object>>? fields) =>
        fields?.ToDictionary(
            field => field.Key,
            field => (IList<object>)(field.Value?.Select(ToClrValue).OfType<object>().ToList() ?? [])) ?? new Dictionary<string, IList<object>>();

    /// <summary>
    /// A number written without a fraction becomes a <see cref="long"/>, any other number a <see cref="double"/>;
    /// strings, booleans and values that are not a <see cref="JsonElement"/> are kept.
    /// </summary>
    public static object? ToClrValue(object? value) => value switch
    {
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
        JsonElement { ValueKind: JsonValueKind.Number } element => element.TryGetInt64(out var number) ? (object)number : element.GetDouble(),
        JsonElement { ValueKind: JsonValueKind.True } => true,
        JsonElement { ValueKind: JsonValueKind.False } => false,
        JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
        JsonElement element => element.Clone(),
        _ => value
    };
}
