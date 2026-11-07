using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using OpenSearch.Client;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services.Queries;

internal sealed class TermSubQueryTranslator : OpenSearchSubQueryTranslator<TermSubQuery>
{
    protected override Task<Result<QueryContainer>> TranslateAsync(TermSubQuery query, IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators)
    {
        var field = OpenSearchFieldNames.Require(query.Field, nameof(TermSubQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<QueryContainer>());
        }

        if (query.Value == null)
        {
            return Task.FromResult(Result.Fail<QueryContainer>($"{nameof(TermSubQuery)} needs a Value."));
        }

        // Text is indexed lower-cased by the default analyzer, so string terms match case-insensitively,
        // as they do in the other providers.
        return Task.FromResult(Result.Ok<QueryContainer>(new TermQuery
        {
            Field = field.Value,
            Value = query.Value,
            CaseInsensitive = query.Value is string ? true : null,
            Boost = OpenSearchFieldNames.Boost(query.BoostValue),
            IsVerbatim = true
        }));
    }
}
