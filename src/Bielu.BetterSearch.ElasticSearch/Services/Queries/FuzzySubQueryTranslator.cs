using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Elastic.Clients.Elasticsearch;
using FluentResults;
using EsFuzzyQuery = Elastic.Clients.Elasticsearch.QueryDsl.FuzzyQuery;
using EsQuery = Elastic.Clients.Elasticsearch.QueryDsl.Query;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.ElasticSearch.Services.Queries;

internal sealed class FuzzySubQueryTranslator : ElasticSearchSubQueryTranslator<FuzzyQuery>
{
    private const int MaxEdits = 2;

    protected override Task<Result<EsQuery>> TranslateAsync(FuzzyQuery query, IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators)
    {
        var field = ElasticSearchFieldNames.Require(query.Field, nameof(FuzzyQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<EsQuery>());
        }

        if (query.Value == null)
        {
            return Task.FromResult(Result.Fail<EsQuery>($"{nameof(FuzzyQuery)} needs a Value."));
        }

        // As in the Lifti provider, the larger number of the pair is the edit distance. Elasticsearch
        // allows at most 2 edits.
        var fuzziness = query.Fuziness == null
            ? new Fuzziness("AUTO")
            : new Fuzziness(Math.Clamp(Math.Max(query.Fuziness.Item1, query.Fuziness.Item2), 0, MaxEdits));

        // A fuzzy query is not analyzed, and text is indexed lower-cased, so string values are lower-cased.
        var value = query.Value is string text ? text.ToLowerInvariant() : query.Value;

        return Task.FromResult(Result.Ok<EsQuery>(new EsFuzzyQuery
        {
            Field = field.Value,
            Value = FieldValue.FromValue(value),
            Fuzziness = fuzziness,
            Transpositions = query.Transposition,
            PrefixLength = query.PrefixLenght > 0 ? query.PrefixLenght : null,
            MaxExpansions = query.MaxExpansions > 0 ? query.MaxExpansions : null,
            Boost = ElasticSearchFieldNames.Boost(query.BoostValue)
        }));
    }
}
