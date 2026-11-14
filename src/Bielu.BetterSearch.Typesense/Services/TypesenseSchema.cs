using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.BetterSearch.Typesense.Configuration;
using FluentResults;
using Microsoft.Extensions.Options;
using N = Bielu.BetterSearch.Typesense.Services.TypesenseFieldNames;

namespace Bielu.BetterSearch.Typesense.Services;

/// <summary>
/// The collection schema the provider creates, and a cache of each collection's fields. Typesense adds a field to the
/// schema when the first document holding it is indexed, and fails a filter or sort on a field it does not have, so a
/// search reads the fields to leave out the ones no document holds. Public only so the providers can take it from
/// dependency injection.
/// </summary>
public sealed class TypesenseSchema
{
    private readonly IOptionsMonitor<TypesenseOptions> optionsMonitor;

    internal TypesenseSchema(IOptionsMonitor<TypesenseOptions> optionsMonitor) => this.optionsMonitor = optionsMonitor;

    private readonly ConcurrentDictionary<(TypesenseHttpClientWrapper Client, string Index), (long CheckedAt, HashSet<string> Fields)> _fields = new();

    /// <summary>The collection's schema: each kind of value the provider stores is a field pattern with its type.</summary>
    internal static JsonObject Create(string index) => new()
    {
        ["name"] = index,
        ["fields"] = new JsonArray(
            Field(N.Type, "string"),
            Field(N.Index, "string", index: false),
            Field(N.Source, "string", index: false),
            Field(N.Tokens + ".*", "string[]"),
            Field(N.Strings + ".*", "string[]"),
            Field(N.Phrases + ".*", "string[]"),
            Field(N.Integers + ".*", "int64[]"),
            Field(N.Numbers + ".*", "float[]"),
            Field(N.Dates + ".*", "int64[]"),
            Field(N.Booleans + ".*", "bool[]"),
            Field(N.Geo + ".*", "geopoint"),
            Field(N.SortInteger + ".*", "int64", sort: true),
            Field(N.SortNumber + ".*", "float", sort: true),
            Field(N.SortString + ".*", "string", sort: true))
    };

    private static JsonObject Field(string name, string type, bool index = true, bool sort = false)
    {
        var field = new JsonObject { ["name"] = name, ["type"] = type, ["optional"] = true };
        if (!index)
        {
            field["index"] = false;
        }

        if (sort)
        {
            field["sort"] = true;
        }

        return field;
    }

    /// <summary>
    /// The fields of <paramref name="index"/>, read again when <paramref name="refresh"/> is set or the last read is older
    /// than <c>TypesenseOptions.SchemaCheckInterval</c>; <c>null</c> when the collection does not exist.
    /// </summary>
    internal async Task<Result<HashSet<string>?>> FieldsAsync(TypesenseHttpClientWrapper client, string index, bool refresh, CancellationToken cancellationToken)
    {
        if (!refresh && _fields.TryGetValue((client, index), out var cached)
                     && Stopwatch.GetElapsedTime(cached.CheckedAt) < optionsMonitor.CurrentValue.SchemaCheckInterval)
        {
            return Result.Ok<HashSet<string>?>(cached.Fields);
        }

        var response = await client.SendAsync(HttpMethod.Get, $"collections/{TypesenseHttpClientWrapper.Escape(index)}", null, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            Forget(client, index);
            return Result.Ok<HashSet<string>?>(null);
        }

        if (!response.IsSuccess)
        {
            return Result.Fail<HashSet<string>?>(response.Describe());
        }

        using var json = JsonDocument.Parse(response.Body);
        var fields = json.RootElement.GetProperty("fields").EnumerateArray()
            .Select(field => field.GetProperty("name").GetString())
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        _fields[(client, index)] = (Stopwatch.GetTimestamp(), fields);
        return Result.Ok<HashSet<string>?>(fields);
    }

    /// <summary>Forgets the fields of <paramref name="index"/>, after it was created or deleted.</summary>
    internal void Forget(TypesenseHttpClientWrapper client, string index) => _fields.TryRemove((client, index), out _);
}
