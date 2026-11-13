namespace Bielu.BetterSearch.Services;

/// <summary>What the shared mapping rules need to know about a provider's field mapping option.</summary>
internal interface IFieldMapping
{
    string? Field { get; }
    bool HasType { get; }
    bool IsText { get; }
    bool IsDate { get; }
    string? Analyzer { get; }
    string? Format { get; }
}

/// <summary>
/// The rules for configured field mappings that Elasticsearch and OpenSearch share. A field mapped as text also
/// gets a <c>keyword</c> sub-field, as dynamic mapping gives it, and sorting on it uses that sub-field, so callers
/// sort on the plain field name. Field names are the query model's names, without the <c>fields.</c> prefix.
/// </summary>
internal static class FieldMappings
{
    /// <summary>The sub-field a text field is sorted on, holding the whole value.</summary>
    public const string KeywordSubField = "keyword";

    /// <summary>
    /// Values longer than this are not stored in the keyword sub-field, as with dynamic mapping, so a document whose
    /// value is longer sorts as if the field were missing.
    /// </summary>
    public const int KeywordIgnoreAbove = 256;

    private const string FieldsPrefix = "fields.";

    /// <summary>The shared mappings with the index's own on top: an index mapping replaces a shared one of the same field.</summary>
    public static List<T> Merge<T>(IEnumerable<T?>? shared, IEnumerable<T?>? own) where T : class, IFieldMapping
    {
        var merged = new List<T>();
        foreach (var mapping in (shared ?? []).Concat(own ?? []))
        {
            if (mapping?.Field is null)
            {
                continue;
            }

            merged.RemoveAll(existing => string.Equals(existing.Field, mapping.Field, StringComparison.Ordinal));
            merged.Add(mapping);
        }

        return merged;
    }

    /// <summary>
    /// The engine field to sort on, given the engine field the provider resolved the sort to: <c>&lt;field&gt;.keyword</c>
    /// for a document field mapped as text, the field itself otherwise. A metadata field, such as <c>_score</c>, has no
    /// <c>fields.</c> prefix, so a document field of the same name never redirects it.
    /// </summary>
    public static string SortField<T>(string resolvedField, IReadOnlyList<T> mappings) where T : IFieldMapping
    {
        if (!resolvedField.StartsWith(FieldsPrefix, StringComparison.Ordinal))
        {
            return resolvedField;
        }

        var name = resolvedField[FieldsPrefix.Length..];
        return mappings.Any(mapping => mapping.IsText && string.Equals(mapping.Field, name, StringComparison.Ordinal))
            ? $"{resolvedField}.{KeywordSubField}"
            : resolvedField;
    }

    /// <summary>The problems with one list of mappings, each naming the setting at <paramref name="path"/>.</summary>
    public static IEnumerable<string> Validate<T>(string path, IList<T?>? mappings) where T : class, IFieldMapping
    {
        if (mappings is null)
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < mappings.Count; i++)
        {
            var setting = $"{path}:Mappings:{i}";
            var mapping = mappings[i];
            if (mapping is null)
            {
                yield return $"{setting} is empty.";
                continue;
            }

            if (string.IsNullOrWhiteSpace(mapping.Field))
            {
                yield return $"{setting}:Field is missing.";
            }
            else if (mapping.Field.StartsWith(FieldsPrefix, StringComparison.Ordinal))
            {
                yield return $"{setting}:Field {mapping.Field} starts with {FieldsPrefix}; write the field name without it.";
            }
            else if (mapping.Field.Trim() != mapping.Field || mapping.Field.StartsWith('.') || mapping.Field.EndsWith('.') || mapping.Field.Contains(".."))
            {
                yield return $"{setting}:Field {mapping.Field} is not a valid field name.";
            }
            else if (!seen.Add(mapping.Field))
            {
                yield return $"{setting}:Field {mapping.Field} is mapped more than once.";
            }

            if (!mapping.HasType)
            {
                yield return $"{setting}:Type is missing or unknown.";
            }

            if (mapping.Analyzer is not null && (string.IsNullOrWhiteSpace(mapping.Analyzer) || !mapping.IsText))
            {
                yield return $"{setting}:Analyzer is set, but only a non-empty analyzer on a Text field is allowed.";
            }

            if (mapping.Format is not null && (string.IsNullOrWhiteSpace(mapping.Format) || !mapping.IsDate))
            {
                yield return $"{setting}:Format is set, but only a non-empty format on a Date field is allowed.";
            }
        }
    }
}
