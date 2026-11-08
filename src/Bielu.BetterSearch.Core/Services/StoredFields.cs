using System.Text.Json;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// Maps a hit's stored fields to <c>CustomProperties</c> the same way on every provider: a field stored as
/// <c>null</c> becomes an empty list, <c>null</c> values are left out and <see cref="JsonElement"/> values become
/// plain values.
/// </summary>
internal static class StoredFields
{
    public static IDictionary<string, List<object>> ToCustomProperties(IDictionary<string, IList<object>>? fields) =>
        fields?.ToDictionary(
            field => field.Key,
            field => field.Value?.Select(ToClrValue).OfType<object>().ToList() ?? []) ?? [];

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
