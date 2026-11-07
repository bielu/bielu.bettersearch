using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using OpenSearch.Client;
using DateRangeQuery = Bielu.BetterSearch.Abstractions.Query.SubQueries.DateRangeQuery;
using LongRange = Bielu.BetterSearch.Abstractions.Query.SubQueries.LongRange;
using OsDateRangeQuery = OpenSearch.Client.DateRangeQuery;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services.Queries;

internal sealed class NumericRangeSubQueryTranslator : OpenSearchSubQueryTranslator<NumericRange>
{
    protected override Task<Result<QueryContainer>> TranslateAsync(NumericRange query, IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators)
    {
        var field = OpenSearchFieldNames.Require(query.Field, nameof(NumericRange));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<QueryContainer>());
        }

        return Task.FromResult(Result.Ok<QueryContainer>(new NumericRangeQuery
        {
            Field = field.Value,
            GreaterThanOrEqualTo = query.IncludeMinEdge ? query.MinValue : null,
            GreaterThan = query.IncludeMinEdge ? null : query.MinValue,
            LessThanOrEqualTo = query.IncludeMaxEdge ? query.MaxValue : null,
            LessThan = query.IncludeMaxEdge ? null : query.MaxValue,
            Boost = OpenSearchFieldNames.Boost(query.BoostValue)
        }));
    }
}

/// <summary>A <c>long</c> range, so values beyond the precision of a double are sent unchanged.</summary>
internal sealed class LongRangeSubQueryTranslator : OpenSearchSubQueryTranslator<LongRange>
{
    protected override Task<Result<QueryContainer>> TranslateAsync(LongRange query, IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators)
    {
        var field = OpenSearchFieldNames.Require(query.Field, nameof(LongRange));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<QueryContainer>());
        }

        return Task.FromResult(Result.Ok<QueryContainer>(new LongRangeQuery
        {
            Field = field.Value,
            GreaterThanOrEqualTo = query.IncludeMinEdge ? query.MinValue : null,
            GreaterThan = query.IncludeMinEdge ? null : query.MinValue,
            LessThanOrEqualTo = query.IncludeMaxEdge ? query.MaxValue : null,
            LessThan = query.IncludeMaxEdge ? null : query.MaxValue,
            Boost = OpenSearchFieldNames.Boost(query.BoostValue)
        }));
    }
}

internal sealed class DateRangeSubQueryTranslator : OpenSearchSubQueryTranslator<DateRangeQuery>
{
    protected override Task<Result<QueryContainer>> TranslateAsync(DateRangeQuery query, IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators)
    {
        var field = OpenSearchFieldNames.Require(query.Field, nameof(DateRangeQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<QueryContainer>());
        }

        var min = query.MinValue is { } minValue ? DateMath.Anchored(minValue) : null;
        var max = query.MaxValue is { } maxValue ? DateMath.Anchored(maxValue) : null;
        return Task.FromResult(Result.Ok<QueryContainer>(new OsDateRangeQuery
        {
            Field = field.Value,
            GreaterThanOrEqualTo = query.IncludeMinEdge ? min : null,
            GreaterThan = query.IncludeMinEdge ? null : min,
            LessThanOrEqualTo = query.IncludeMaxEdge ? max : null,
            LessThan = query.IncludeMaxEdge ? null : max,
            Boost = OpenSearchFieldNames.Boost(query.BoostValue)
        }));
    }
}

/// <summary>A lexicographic range over the terms of the field; use a keyword field for whole values.</summary>
internal sealed class StringRangeSubQueryTranslator : OpenSearchSubQueryTranslator<StringRange>
{
    protected override Task<Result<QueryContainer>> TranslateAsync(StringRange query, IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators)
    {
        var field = OpenSearchFieldNames.Require(query.Field, nameof(StringRange));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<QueryContainer>());
        }

        return Task.FromResult(Result.Ok<QueryContainer>(new TermRangeQuery
        {
            Field = field.Value,
            GreaterThanOrEqualTo = query.IncludeMinEdge ? query.MinValue : null,
            GreaterThan = query.IncludeMinEdge ? null : query.MinValue,
            LessThanOrEqualTo = query.IncludeMaxEdge ? query.MaxValue : null,
            LessThan = query.IncludeMaxEdge ? null : query.MaxValue,
            Boost = OpenSearchFieldNames.Boost(query.BoostValue)
        }));
    }
}
