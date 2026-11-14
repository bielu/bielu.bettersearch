using System.Globalization;
using System.Text.Json;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using FluentResults;

namespace Bielu.BetterSearch.Caching;

/// <summary>Turns a search result into its <see cref="CachedSearchResult"/> shape and back.</summary>
internal static class CachedSearchResults
{
    /// <summary>
    /// The cached shape of <paramref name="result"/>, or <c>null</c> when a hit holds a field value of a type that
    /// <see cref="CachedValueKind"/> can't carry, so the result can't be cached without changing it.
    /// </summary>
    public static CachedSearchResult? From(Result<ISearchResult> result)
    {
        var search = result.Value;
        var cached = new CachedSearchResult
        {
            TotalCount = search.Pagination?.TotalCount ?? 0,
            Page = search.Pagination?.Page ?? 0,
            PageSize = search.Pagination?.PageSize ?? 0,
            TotalPages = search.Pagination?.TotalPages ?? 0,
            Warnings = result.Successes.OfType<TranslationWarning>().Select(warning => warning.Message).ToList()
        };

        foreach (var hit in search.Items ?? [])
        {
            var fields = new Dictionary<string, List<CachedValue>>();
            foreach (var (name, values) in hit.Document?.CustomProperties ?? new Dictionary<string, List<object>>())
            {
                var cachedValues = new List<CachedValue>();
                foreach (var value in values ?? [])
                {
                    var cachedValue = ToCachedValue(value);
                    if (cachedValue is null)
                    {
                        return null;
                    }

                    cachedValues.Add(cachedValue);
                }

                fields[name] = cachedValues;
            }

            cached.Items.Add(new CachedSearchHit
            {
                Id = hit.Document?.Id,
                ContentType = hit.Document?.ContentType,
                Score = hit.Score?.ToString("R", CultureInfo.InvariantCulture),
                Fields = fields,
                Highlights = hit.Highlights.ToDictionary(highlight => highlight.Key, highlight => highlight.Value.ToList())
            });
        }

        return cached;
    }

    /// <summary>The search result <paramref name="cached"/> holds, with its warnings.</summary>
    public static Result<ISearchResult> ToResult(CachedSearchResult cached)
    {
        var hits = cached.Items.Select(hit => (ISearchHit)new SearchHit(
            new BaseSearchModel
            {
                Id = hit.Id!,
                ContentType = hit.ContentType!,
                CustomProperties = hit.Fields.ToDictionary(
                    field => field.Key,
                    // A cached null stays null, as it was in the hit the cache was filled from.
                    field => field.Value.Select(value => FromCachedValue(value)!).ToList())
            },
            hit.Score is null ? null : double.Parse(hit.Score, NumberStyles.Float, CultureInfo.InvariantCulture),
            hit.Highlights.Count == 0
                ? null
                : hit.Highlights.ToDictionary(highlight => highlight.Key,
                    highlight => (IReadOnlyList<string>)highlight.Value.AsReadOnly()))).ToList();

        ISearchResult search = new BaseSearchResults
        {
            Items = hits,
            Pagination = new Pagination
            {
                TotalCount = cached.TotalCount,
                Page = cached.Page,
                PageSize = cached.PageSize,
                TotalPages = cached.TotalPages
            }
        };

        var result = Result.Ok(search);
        foreach (var warning in cached.Warnings)
        {
            result.WithSuccess(new TranslationWarning(warning));
        }

        return result;
    }

    private static CachedValue? ToCachedValue(object? value) => value switch
    {
        null => Cached(CachedValueKind.Null, string.Empty),
        string text => Cached(CachedValueKind.String, text),
        bool flag => Cached(CachedValueKind.Boolean, flag ? "true" : "false"),
        int number => Cached(CachedValueKind.Int32, number.ToString(CultureInfo.InvariantCulture)),
        long number => Cached(CachedValueKind.Int64, number.ToString(CultureInfo.InvariantCulture)),
        double number => Cached(CachedValueKind.Double, number.ToString("R", CultureInfo.InvariantCulture)),
        float number => Cached(CachedValueKind.Single, number.ToString("R", CultureInfo.InvariantCulture)),
        decimal number => Cached(CachedValueKind.Decimal, number.ToString(CultureInfo.InvariantCulture)),
        DateTime date => Cached(CachedValueKind.DateTime, date.ToString("O", CultureInfo.InvariantCulture)),
        DateTimeOffset date => Cached(CachedValueKind.DateTimeOffset, date.ToString("O", CultureInfo.InvariantCulture)),
        DateOnly date => Cached(CachedValueKind.DateOnly, date.ToString("O", CultureInfo.InvariantCulture)),
        JsonElement element => Cached(CachedValueKind.Json, element.GetRawText()),
        _ => null
    };

    private static CachedValue Cached(CachedValueKind kind, string text) => new() { Kind = kind, Text = text };

    private static object? FromCachedValue(CachedValue value) => value.Kind switch
    {
        CachedValueKind.Null => null,
        CachedValueKind.String => value.Text,
        CachedValueKind.Boolean => value.Text == "true",
        CachedValueKind.Int32 => int.Parse(value.Text, CultureInfo.InvariantCulture),
        CachedValueKind.Int64 => long.Parse(value.Text, CultureInfo.InvariantCulture),
        CachedValueKind.Double => double.Parse(value.Text, NumberStyles.Float, CultureInfo.InvariantCulture),
        CachedValueKind.Single => float.Parse(value.Text, NumberStyles.Float, CultureInfo.InvariantCulture),
        CachedValueKind.Decimal => decimal.Parse(value.Text, NumberStyles.Number, CultureInfo.InvariantCulture),
        CachedValueKind.DateTime => DateTime.Parse(value.Text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        CachedValueKind.DateTimeOffset => DateTimeOffset.Parse(value.Text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        CachedValueKind.DateOnly => DateOnly.ParseExact(value.Text, "O", CultureInfo.InvariantCulture),
        CachedValueKind.Json => ParseJson(value.Text),
        _ => throw new InvalidOperationException($"A cached value has the unknown kind {value.Kind}.")
    };

    private static JsonElement ParseJson(string text)
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }
}
