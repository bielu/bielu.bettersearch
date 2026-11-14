namespace Bielu.BetterSearch.Caching;

/// <summary>
/// The shape the search cache decorator stores a result in: plain data, so it round-trips through the serializer of a
/// distributed (L2) cache, such as System.Text.Json. Internal: an <c>ISearchResultCache</c> only sees it as the opaque
/// <c>T</c> of <c>GetOrCreateAsync</c>. Each field value keeps its type in <see cref="CachedValue.Kind"/>.
/// </summary>
internal sealed class CachedSearchResult
{
    public List<CachedSearchHit> Items { get; set; } = [];
    public long TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }

    /// <summary>The messages of the result's <c>TranslationWarning</c> reasons.</summary>
    public List<string> Warnings { get; set; } = [];
}

/// <summary>One cached hit.</summary>
internal sealed class CachedSearchHit
{
    public string? Id { get; set; }
    public string? ContentType { get; set; }

    /// <summary>The score as round-trip text, so <c>NaN</c> survives serializers that reject it as a number.</summary>
    public string? Score { get; set; }

    public Dictionary<string, List<CachedValue>> Fields { get; set; } = [];
    public Dictionary<string, List<string>> Highlights { get; set; } = [];
}

/// <summary>One field value, as round-trip text and the type to read it back as.</summary>
internal sealed class CachedValue
{
    public CachedValueKind Kind { get; set; }
    public string Text { get; set; } = string.Empty;
}

/// <summary>The types of field value a result can be cached with. A result holding any other type is not cached.</summary>
internal enum CachedValueKind
{
    String = 0,
    Boolean = 1,
    Int32 = 2,
    Int64 = 3,
    Double = 4,
    Single = 5,
    Decimal = 6,
    DateTime = 7,
    DateTimeOffset = 8,
    DateOnly = 9,
    Json = 10,
    Null = 11
}
