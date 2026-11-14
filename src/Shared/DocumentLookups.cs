using FluentResults;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// Checks the arguments of a get, an update and a count the same way in the services and every provider, and copies
/// a stored document's fields into the shape <see cref="SearchDocument.Fields"/> holds.
/// </summary>
internal static class DocumentLookups
{
    public static Result CheckGet(string? index, string? id)
    {
        if (string.IsNullOrWhiteSpace(index))
        {
            return Result.Fail("GetDocumentAsync needs an index.");
        }

        return string.IsNullOrEmpty(id) ? Result.Fail("GetDocumentAsync needs a document id.") : Result.Ok();
    }

    /// <summary>
    /// Fails when the index or id is missing, or when <paramref name="fields"/> is <c>null</c>, empty, or holds a
    /// <c>null</c> list or a <c>null</c> value, so a malformed update fails the same way on every provider.
    /// </summary>
    public static Result CheckUpdate(string? index, string? id, IDictionary<string, IList<object>>? fields)
    {
        if (string.IsNullOrWhiteSpace(index))
        {
            return Result.Fail("UpdateDocumentAsync needs an index.");
        }

        if (string.IsNullOrEmpty(id))
        {
            return Result.Fail("UpdateDocumentAsync needs a document id.");
        }

        if (fields is null || fields.Count == 0)
        {
            return Result.Fail($"The update of document {id} has no fields; pass at least one field to change.");
        }

        var nullFields = fields.Where(field => field.Value is null).Select(field => field.Key).ToList();
        if (nullFields.Count > 0)
        {
            return Result.Fail($"The update of document {id} has null fields ({string.Join(", ", nullFields)}); use an empty list for a field with no values.");
        }

        var nullValueFields = fields.Where(field => field.Value.Any(value => value is null)).Select(field => field.Key).ToList();
        return nullValueFields.Count > 0
            ? Result.Fail($"The update of document {id} has null values in fields ({string.Join(", ", nullValueFields)}); leave them out of the list.")
            : Result.Ok();
    }

    public static Result CheckCount(string? index) =>
        string.IsNullOrWhiteSpace(index) ? Result.Fail("The query needs an Index.") : Result.Ok();

    /// <summary>A copy of <paramref name="fields"/>, a <c>null</c> map giving an empty one.</summary>
    public static IDictionary<string, IList<object>> ToFields(IDictionary<string, List<object>>? fields) =>
        fields?.ToDictionary(field => field.Key, field => (IList<object>)field.Value) ?? new Dictionary<string, IList<object>>();
}
