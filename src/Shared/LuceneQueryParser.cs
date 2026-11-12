using FluentResults;
using Lucene.Net.Analysis.Core;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Util;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// Parses Lucene query syntax with Lucene's own classic parser, the syntax an Elasticsearch <c>query_string</c>
/// accepts, so a provider can translate the tree. Terms are split on whitespace only and keep their case, so the
/// provider decides how values match.
/// </summary>
internal static class LuceneQueryParser
{
    /// <summary>The field of a term written without one, which no field name a query can name equals.</summary>
    public const string NoField = "\0";

    public static Result<Query> Parse(string? text, string queryName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result.Fail<Query>($"{queryName} needs a query.");
        }

        try
        {
            var parser = new QueryParser(LuceneVersion.LUCENE_48, NoField, new WhitespaceAnalyzer(LuceneVersion.LUCENE_48))
            {
                AllowLeadingWildcard = true,
                LowercaseExpandedTerms = false
            };
            return Result.Ok(parser.Parse(text));
        }
        catch (Exception e) when (e is ParseException or BooleanQuery.TooManyClausesException)
        {
            return Result.Fail<Query>(new Error($"{queryName} '{text}' is not valid Lucene syntax: {e.Message}").CausedBy(e));
        }
    }
}
