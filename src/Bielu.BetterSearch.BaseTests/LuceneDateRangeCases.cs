namespace Bielu.BetterSearch.Tests;

/// <summary>
/// Lucene ranges over dates written without a time, with what Elasticsearch's <c>query_string</c> returns for them, so
/// a provider that translates Lucene itself can check it rounds the same way.
/// </summary>
public static class LuceneDateRangeCases
{
    /// <summary>Documents born at the first and the last minute of the days the ranges start or end on, and one between.</summary>
    public static SearchDocument[] Documents(string index) =>
    [
        Born(index, "a", new DateTime(2025, 12, 31, 23, 59, 0, DateTimeKind.Utc)),
        Born(index, "b", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
        Born(index, "c", new DateTime(2026, 1, 1, 23, 59, 0, DateTimeKind.Utc)),
        Born(index, "d", new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc)),
        Born(index, "e", new DateTime(2026, 1, 31, 23, 59, 0, DateTimeKind.Utc)),
        Born(index, "f", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)),
        Born(index, "m", new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc))
    ];

    /// <summary>The range and the ids Elasticsearch returns for it, checked against the AppHost's Elasticsearch.</summary>
    public static TheoryData<string, string[]> Ranges => new()
    {
        // An included maximum and an excluded minimum round up to the end of the day; the other bounds do not.
        { "fields.Born:[2026-01-01 TO 2026-01-31]", ["b", "c", "d", "e", "m"] },
        { "fields.Born:{2026-01-01 TO 2026-01-31}", ["m"] },
        { "fields.Born:{2026-01-01 TO *]", ["d", "e", "f", "m"] },
        { "fields.Born:[2026-01-31 TO *]", ["d", "e", "f"] },
        { "fields.Born:[* TO 2026-01-01}", ["a"] },
        { "fields.Born:[* TO 2026-01-01]", ["a", "b", "c"] },
    };

    private static SearchDocument Born(string index, string id, DateTime born) => new()
    {
        Id = id,
        Index = index,
        Fields = new Dictionary<string, IList<object>> { { "Born", [born] } }
    };
}
