using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using OpenSearch.Client;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services.Queries;

/// <summary>
/// Matches any of the terms with one <c>terms</c> query. It has no case-insensitive option, so each
/// string is also sent lower-cased: that matches the tokens the default analyzer indexes for text,
/// while a keyword field still matches the value as given.
/// </summary>
internal sealed class TermsSubQueryTranslator : OpenSearchSubQueryTranslator<TermsSubQuery>
{
    /// <summary>The default <c>index.max_terms_count</c>; a longer list is rejected by OpenSearch.</summary>
    public const int MaxTermsCount = 65_536;

    protected override Task<Result<QueryContainer>> TranslateAsync(TermsSubQuery query, IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators)
    {
        var field = OpenSearchFieldNames.Require(query.Field, nameof(TermsSubQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<QueryContainer>());
        }

        var values = query.Value?.Where(value => value != null).ToList() ?? [];
        if (values.Count == 0)
        {
            return Task.FromResult(Result.Ok<QueryContainer>(new MatchNoneQuery()));
        }

        var terms = values
            .SelectMany(value => value is string text ? [text, text.ToLowerInvariant()] : new[] { value })
            .Distinct()
            .ToList();
        if (terms.Count > MaxTermsCount)
        {
            return Task.FromResult(Result.Fail<QueryContainer>(
                $"The terms query on {query.Field} sends {terms.Count} terms, each string as given and lower-cased, past the limit of {MaxTermsCount} (index.max_terms_count)."));
        }

        return Task.FromResult(Result.Ok<QueryContainer>(new TermsQuery
        {
            Field = field.Value,
            Terms = terms,
            Boost = OpenSearchFieldNames.Boost(query.BoostValue),
            IsVerbatim = true
        }));
    }
}
