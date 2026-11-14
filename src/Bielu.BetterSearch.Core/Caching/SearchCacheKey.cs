using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Services;

namespace Bielu.BetterSearch.Caching;

/// <summary>
/// Builds the cache key of a search from everything that changes its result: the provider, the application's own key
/// part and the whole query, written out canonically and hashed.
/// </summary>
internal static class SearchCacheKey
{
    private const int MaxDepth = 64;

    /// <summary>The key of <paramref name="query"/>, or <c>null</c> when it holds something with no canonical text.</summary>
    public static string? TryBuild(ISearchQuery query, string provider, string? keyPart)
    {
        try
        {
            return Build(query, provider, keyPart);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>The key of <paramref name="query"/> run by <paramref name="provider"/>.</summary>
    public static string Build(ISearchQuery query, string provider, string? keyPart)
    {
        var text = new StringBuilder();
        WriteString(text, provider);
        text.Append('|');
        WriteString(text, keyPart);
        text.Append('|');
        Write(text, query, 0);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        return "bettersearch:search:" + Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// The canonical text of <paramref name="value"/>: its runtime type and every public property, in name order, with
    /// dictionaries in key order, lists and tuples in their own order, JSON as its text and scalar values tagged with their
    /// type and written with <see cref="QueryValueText"/>, so <c>1</c> and <c>"1"</c> differ while the machine's culture
    /// changes nothing. Any other type outside the query model throws, as its public properties may not hold its state.
    /// </summary>
    internal static string Canonical(object? value)
    {
        var text = new StringBuilder();
        Write(text, value, 0);
        return text.ToString();
    }

    private static void Write(StringBuilder text, object? value, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new InvalidOperationException($"The query nests more than {MaxDepth} levels deep, so it has no cache key.");
        }

        switch (value)
        {
            case null:
                text.Append("null");
                return;
            case string s:
                WriteString(text, s);
                return;
            case Enum e:
                text.Append(e.GetType().FullName).Append(':').Append(e.ToString());
                return;
            case Delegate:
                throw new InvalidOperationException("A query holding a delegate has no cache key.");
            // JSON keeps its contents out of its public properties, so it is written as its JSON text.
            case JsonElement { ValueKind: JsonValueKind.Undefined }:
                text.Append("JsonElement:undefined");
                return;
            case JsonElement element:
                text.Append("JsonElement:");
                WriteString(text, element.GetRawText());
                return;
            case JsonNode node:
                text.Append("JsonNode:");
                WriteString(text, node.ToJsonString());
                return;
        }

        var type = value.GetType();
        if (type.IsPrimitive || value is decimal or DateTime or DateTimeOffset or DateOnly or TimeOnly or TimeSpan or Guid)
        {
            text.Append(type.Name).Append(':');
            WriteString(text, QueryValueText.From(value));
            return;
        }

        if (value is IDictionary dictionary)
        {
            var entries = new List<(string Key, object? Value)>();
            foreach (DictionaryEntry entry in dictionary)
            {
                entries.Add((Canonical(entry.Key), entry.Value));
            }

            text.Append('{');
            foreach (var (key, item) in entries.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                text.Append(key).Append('=');
                Write(text, item, depth + 1);
                text.Append(';');
            }

            text.Append('}');
            return;
        }

        if (value is ITuple tuple)
        {
            text.Append(type.FullName).Append('(');
            for (var i = 0; i < tuple.Length; i++)
            {
                Write(text, tuple[i], depth + 1);
                text.Append(',');
            }

            text.Append(')');
            return;
        }

        if (value is IEnumerable sequence)
        {
            text.Append('[');
            foreach (var item in sequence)
            {
                Write(text, item, depth + 1);
                text.Append(',');
            }

            text.Append(']');
            return;
        }

        // Public properties are the whole state only of the query model; another type can keep what makes it differ
        // out of them, as JsonElement does, so a query holding one is not cached.
        if (!IsQueryModel(type))
        {
            throw new InvalidOperationException($"A query holding a {type.FullName} has no cache key.");
        }

        text.Append(type.FullName).Append('(');
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
                     .OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            text.Append(property.Name).Append('=');
            Write(text, property.GetValue(value), depth + 1);
            text.Append(';');
        }

        text.Append(')');
    }

    /// <summary>Whether <paramref name="type"/> is part of the query model: declared in Abstractions, or implementing one of its interfaces.</summary>
    private static bool IsQueryModel(Type type)
    {
        var abstractions = typeof(ISearchQuery).Assembly;
        return type.Assembly == abstractions || type.GetInterfaces().Any(contract => contract.Assembly == abstractions);
    }

    private static void WriteString(StringBuilder text, string? value)
    {
        if (value is null)
        {
            text.Append("null");
            return;
        }

        // Length-prefixed, so no value can end early and run into the next.
        text.Append(value.Length).Append('"').Append(value).Append('"');
    }
}
