using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.Highlighter;
using FluentResults;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// Reads a query's <c>HighlightQueries</c> and maps the fragments an engine returns to a hit's highlights, the same
/// way for every provider that highlights.
/// </summary>
internal static class Highlights
{
    /// <summary>The field name a highlight request on every field is listed under.</summary>
    public const string AllFields = "*";

    /// <summary>
    /// The highlight requests of <paramref name="query"/>, each with the field it names, or <see cref="AllFields"/>
    /// when <c>IsAllField</c> is set. Fails on a <c>null</c> entry, a request with neither a field nor
    /// <c>IsAllField</c>, and two requests for the same field.
    /// </summary>
    public static Result<IReadOnlyList<(string Field, IHighlightQuery Request)>> Requested(ISearchQuery query)
    {
        var requests = new List<(string Field, IHighlightQuery Request)>();
        var fields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var request in query.HighlightQueries ?? [])
        {
            if (request == null)
            {
                return Result.Fail<IReadOnlyList<(string, IHighlightQuery)>>("HighlightQueries holds a null entry.");
            }

            if (!request.IsAllField && string.IsNullOrWhiteSpace(request.Field))
            {
                return Result.Fail<IReadOnlyList<(string, IHighlightQuery)>>("A highlight query needs a Field, or IsAllField set to true.");
            }

            var field = request.IsAllField ? AllFields : request.Field;
            if (!fields.Add(field))
            {
                return Result.Fail<IReadOnlyList<(string, IHighlightQuery)>>(
                    $"HighlightQueries names {(request.IsAllField ? "all fields" : $"field {field}")} more than once.");
            }

            requests.Add((field, request));
        }

        return Result.Ok<IReadOnlyList<(string, IHighlightQuery)>>(requests);
    }

    /// <summary>The clauses of a request's own highlight query, grouped as the main query's are, or <c>null</c> when it has none.</summary>
    public static IDictionary<Occurance, List<ISearchSubQuery>>? OwnQuery(IHighlightQuery request)
    {
        var clauses = (request.Queries ?? [])
            .Where(clause => clause.Value != null)
            .ToDictionary(clause => clause.Key, clause => new List<ISearchSubQuery> { clause.Value });
        return clauses.Count > 0 ? clauses : null;
    }

    /// <summary>
    /// The fragments an engine returned, keyed by the query model's field name, which <paramref name="fieldName"/> gives
    /// for an engine field. A field with no fragments is left out; <c>null</c> when nothing is left.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>>? ToHit<TFragments>(
        IReadOnlyDictionary<string, TFragments>? engineHighlights,
        Func<string, string> fieldName)
        where TFragments : IEnumerable<string>
    {
        if (engineHighlights is not { Count: > 0 })
        {
            return null;
        }

        var highlights = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var (field, fragments) in engineHighlights)
        {
            var list = fragments?.Where(fragment => !string.IsNullOrEmpty(fragment)).ToList();
            if (list is { Count: > 0 })
            {
                highlights[fieldName(field)] = list;
            }
        }

        return highlights.Count > 0 ? highlights : null;
    }
}
