using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using FluentResults;
using OpenSearch.Client;
using FuzzyQuery = Bielu.BetterSearch.Abstractions.Query.SubQueries.FuzzyQuery;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services.Queries;

internal sealed class FuzzySubQueryTranslator : OpenSearchSubQueryTranslator<FuzzyQuery>
{
    private const int MaxEdits = 2;

    protected override Task<Result<QueryContainer>> TranslateAsync(FuzzyQuery query, IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators)
    {
        var field = OpenSearchFieldNames.Require(query.Field, nameof(FuzzyQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<QueryContainer>());
        }

        if (query.Value == null)
        {
            return Task.FromResult(Result.Fail<QueryContainer>($"{nameof(FuzzyQuery)} needs a Value."));
        }

        // A date or boolean is not text to match within a few edits: a date field rejects a fuzzy match at search
        // time, and a text field splits a date into numbers that fuzzily match unrelated documents.
        if (query.Value is not string && !QueryValueText.IsNumber(query.Value))
        {
            return Task.FromResult(Result.Fail<QueryContainer>(
                $"{nameof(FuzzyQuery)} value '{QueryValueText.From(query.Value)}' is a {query.Value.GetType().Name}; a fuzzy match needs a string or a number."));
        }

        // As in the other providers, the larger number of the pair is the edit distance. OpenSearch
        // allows at most 2 edits.
        var fuzziness = query.Fuziness == null
            ? Fuzziness.Auto
            : Fuzziness.EditDistance(Math.Clamp(Math.Max(query.Fuziness.Item1, query.Fuziness.Item2), 0, MaxEdits));

        // A match query runs the value through the field's own search analyzer, so a text field
        // matches it lower-cased and a keyword field matches it as given. Each token is matched fuzzily,
        // and every token has to match.
        return Task.FromResult(Result.Ok<QueryContainer>(new MatchQuery
        {
            Field = field.Value,
            Query = QueryValueText.From(query.Value),
            Operator = Operator.And,
            Fuzziness = fuzziness,
            FuzzyTranspositions = query.Transposition,
            PrefixLength = query.PrefixLenght > 0 ? query.PrefixLenght : null,
            MaxExpansions = query.MaxExpansions > 0 ? query.MaxExpansions : null,
            Boost = OpenSearchFieldNames.Boost(query.BoostValue),
            IsVerbatim = true
        }));
    }
}
