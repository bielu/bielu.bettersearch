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
    /// The highlight requests of <paramref name="query"/>, each with the engine field <paramref name="resolve"/> gives
    /// for the field it names, or for <see cref="AllFields"/> when <c>IsAllField</c> is set. Fails on a <c>null</c>
    /// entry, a request with neither a field nor <c>IsAllField</c>, and two requests that resolve to the same engine
    /// field, such as <c>Name</c> and <c>fields.Name</c>.
    /// </summary>
    public static Result<IReadOnlyList<(string Field, IHighlightQuery Request)>> Requested(
        ISearchQuery query,
        Func<string, string> resolve)
    {
        var requests = new List<(string Field, IHighlightQuery Request)>();
        var named = new Dictionary<string, string>(StringComparer.Ordinal);
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

            var name = request.IsAllField ? "all fields" : $"field {request.Field}";
            var field = resolve(request.IsAllField ? AllFields : request.Field);
            if (!named.TryAdd(field, name))
            {
                var first = named[field];
                return Result.Fail<IReadOnlyList<(string, IHighlightQuery)>>(
                    $"HighlightQueries names {first} more than once{(first == name ? string.Empty : $", as {name}")}.");
            }

            requests.Add((field, request));
        }

        return Result.Ok<IReadOnlyList<(string, IHighlightQuery)>>(requests);
    }

    /// <summary>
    /// The clauses of a request's own highlight query, grouped as the main query's are, or <c>null</c> when it has
    /// none. Fails on a <c>null</c> clause, so a malformed own query is not mistaken for no own query.
    /// </summary>
    public static Result<IDictionary<Occurance, List<ISearchSubQuery>>?> OwnQuery(IHighlightQuery request)
    {
        var clauses = new Dictionary<Occurance, List<ISearchSubQuery>>();
        foreach (var (occurance, clause) in request.Queries ?? [])
        {
            if (clause == null)
            {
                return Result.Fail<IDictionary<Occurance, List<ISearchSubQuery>>?>(
                    $"The highlight query for {(request.IsAllField ? "all fields" : $"field {request.Field}")} holds a null {occurance} clause.");
            }

            clauses[occurance] = [clause];
        }

        return Result.Ok<IDictionary<Occurance, List<ISearchSubQuery>>?>(clauses.Count > 0 ? clauses : null);
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
