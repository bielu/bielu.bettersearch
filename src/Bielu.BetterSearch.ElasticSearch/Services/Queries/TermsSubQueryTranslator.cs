using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FluentResults;
using EsQuery = Elastic.Clients.Elasticsearch.QueryDsl.Query;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.ElasticSearch.Services.Queries;

/// <summary>
/// Matches any of the terms with one <c>terms</c> query. It has no case-insensitive option, so each
/// string is also sent lower-cased: that matches the tokens the default analyzer indexes for text,
/// while a keyword field still matches the value as given.
/// </summary>
internal sealed class TermsSubQueryTranslator : ElasticSearchSubQueryTranslator<TermsSubQuery>
{
    protected override Task<Result<EsQuery>> TranslateAsync(TermsSubQuery query, IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators)
    {
        var field = ElasticSearchFieldNames.Require(query.Field, nameof(TermsSubQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<EsQuery>());
        }

        var values = query.Value?.Where(value => value != null).ToList() ?? [];
        if (values.Count == 0)
        {
            return Task.FromResult(Result.Ok<EsQuery>(new MatchNoneQuery()));
        }

        var terms = values
            .SelectMany(value => value is string text ? [text, text.ToLowerInvariant()] : new[] { value })
            .Distinct()
            .Select(FieldValue.FromValue)
            .ToList();
        return Task.FromResult(Result.Ok<EsQuery>(new TermsQuery
        {
            Field = field.Value,
            Terms = new TermsQueryField(terms),
            Boost = ElasticSearchFieldNames.Boost(query.BoostValue)
        }));
    }
}
