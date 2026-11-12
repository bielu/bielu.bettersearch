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

    /// <summary>
    /// The deepest nesting of parentheses a query may have. The classic parser recurses once per group, and a stack
    /// overflow ends the process instead of throwing, so deeper queries are refused before parsing.
    /// </summary>
    public const int MaxDepth = 32;

    public static Result<Query> Parse(string? text, string queryName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result.Fail<Query>($"{queryName} needs a query.");
        }

        if (Depth(text) > MaxDepth)
        {
            return Result.Fail<Query>($"{queryName} nests parentheses more than {MaxDepth} deep, the most it may.");
        }

        try
        {
            var parser = new PhraseQueryParser(LuceneVersion.LUCENE_48, NoField, new WhitespaceAnalyzer(LuceneVersion.LUCENE_48))
            {
                AllowLeadingWildcard = true,
                LowercaseExpandedTerms = false
            };
            return Result.Ok(parser.Parse(text));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<Query>(new Error($"{queryName} '{text}' is not valid Lucene syntax: {e.Message}").CausedBy(e));
        }
    }

    /// <summary>The deepest nesting of parentheses outside quotes and escapes.</summary>
    private static int Depth(string text)
    {
        var depth = 0;
        var max = 0;
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '\\':
                    i++;
                    break;
                case '"':
                    quoted = !quoted;
                    break;
                case '(' when !quoted:
                    max = Math.Max(max, ++depth);
                    break;
                case ')' when !quoted:
                    depth--;
                    break;
            }
        }

        return max;
    }

    /// <summary>
    /// Keeps quoted text a <see cref="PhraseQuery"/> even when the analyzer finds one term or none, so a provider that
    /// splits words differently still sees a phrase, and an empty phrase is not dropped from its query.
    /// </summary>
    private sealed class PhraseQueryParser(LuceneVersion version, string field, Lucene.Net.Analysis.Analyzer analyzer)
        : QueryParser(version, field, analyzer)
    {
        protected override Query GetFieldQuery(string field, string queryText, bool quoted)
        {
            var query = base.GetFieldQuery(field, queryText, quoted);
            if (!quoted)
            {
                return query;
            }

            return query switch
            {
                TermQuery term => new PhraseQuery { term.Term },
                null => new PhraseQuery(),
                _ => query
            };
        }
    }
}
