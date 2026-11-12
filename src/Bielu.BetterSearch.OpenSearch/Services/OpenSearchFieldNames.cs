using FluentResults;

namespace Bielu.BetterSearch.OpenSearch.Services;

/// <summary>
/// Maps the field names of the query model to OpenSearch fields. Documents are stored with their
/// values under <c>fields</c>, so <c>Name</c> becomes <c>fields.Name</c>. OpenSearch metadata
/// fields, such as <c>_id</c> or <c>_score</c>, pass through; any other name starting with an
/// underscore, such as <c>_tags</c>, is a document field.
/// </summary>
internal static class OpenSearchFieldNames
{
    private const string FieldsPrefix = "fields.";

    private static readonly HashSet<string> MetadataFields = new(StringComparer.Ordinal)
    {
        "_id", "_index", "_score", "_doc", "_source", "_routing", "_ignored", "_seq_no", "_primary_term",
        "_version", "_field_names", "_doc_count", "_size"
    };

    public static string Resolve(string field) =>
        MetadataFields.Contains(field) || field.StartsWith(FieldsPrefix, StringComparison.Ordinal)
            ? field
            : FieldsPrefix + field;

    /// <summary>The query model's name for an OpenSearch field, the reverse of <see cref="Resolve"/>.</summary>
    public static string ToModel(string field) =>
        field.StartsWith(FieldsPrefix, StringComparison.Ordinal) ? field[FieldsPrefix.Length..] : field;

    public static Result<string> Require(string? field, string queryName) =>
        string.IsNullOrWhiteSpace(field)
            ? Result.Fail<string>($"{queryName} needs a Field for OpenSearch.")
            : Result.Ok(Resolve(field));

    /// <summary>The model's default boost is 1, and 0 means unset, so only other values are sent.</summary>
    public static double? Boost(int boostValue) => boostValue is > 0 and not 1 ? boostValue : null;
}
