using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using OpenSearch.Client;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services.Queries;

internal sealed class PrefixSubQueryTranslator : OpenSearchSubQueryTranslator<PrefixSubQuery>
{
    protected override Task<Result<QueryContainer>> TranslateAsync(PrefixSubQuery query, IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators)
    {
        var field = OpenSearchFieldNames.Require(query.Field, nameof(PrefixSubQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<QueryContainer>());
        }

        var value = query.Value?.ToString();
        if (string.IsNullOrEmpty(value))
        {
            return Task.FromResult(Result.Fail<QueryContainer>($"{nameof(PrefixSubQuery)} needs a Value."));
        }

        return Task.FromResult(Result.Ok<QueryContainer>(new PrefixQuery
        {
            Field = field.Value,
            Value = value,
            CaseInsensitive = true,
            Boost = OpenSearchFieldNames.Boost(query.BoostValue),
            IsVerbatim = true
        }));
    }
}
