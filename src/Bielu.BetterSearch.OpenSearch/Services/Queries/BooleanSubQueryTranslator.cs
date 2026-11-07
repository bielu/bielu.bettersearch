using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using OpenSearch.Client;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services.Queries;

/// <summary>Each nested query is a clause of the <c>bool</c> query, with its own occurrence.</summary>
internal sealed class BooleanSubQueryTranslator : OpenSearchSubQueryTranslator<BoolSearchSubQuery>
{
    protected override Task<Result<QueryContainer>> TranslateAsync(BoolSearchSubQuery query, IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators) =>
        OpenSearchBoolQueryBuilder.BuildAsync(
            query.NestedQueries.Select(nested => (nested.Occurance, nested)),
            subQueryTranslators,
            query.BoostValue);
}
