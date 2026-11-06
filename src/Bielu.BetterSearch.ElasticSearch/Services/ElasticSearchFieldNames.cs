using FluentResults;

namespace Bielu.BetterSearch.ElasticSearch.Services;

/// <summary>
/// Maps the field names of the query model to Elasticsearch fields. Documents are stored with their
/// values under <c>fields</c>, so <c>Name</c> becomes <c>fields.Name</c>. Names starting with an
/// underscore are Elasticsearch metadata fields, such as <c>_id</c> or <c>_score</c>, and pass through.
/// </summary>
internal static class ElasticSearchFieldNames
{
    private const string FieldsPrefix = "fields.";

    public static string Resolve(string field) =>
        field.StartsWith('_') || field.StartsWith(FieldsPrefix, StringComparison.Ordinal)
            ? field
            : FieldsPrefix + field;

    public static Result<string> Require(string? field, string queryName) =>
        string.IsNullOrWhiteSpace(field)
            ? Result.Fail<string>($"{queryName} needs a Field for Elasticsearch.")
            : Result.Ok(Resolve(field));

    /// <summary>The model's default boost is 1, and 0 means unset, so only other values are sent.</summary>
    public static float? Boost(int boostValue) => boostValue is > 0 and not 1 ? boostValue : null;
}
