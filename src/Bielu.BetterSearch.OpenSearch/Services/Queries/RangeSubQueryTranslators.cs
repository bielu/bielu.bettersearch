using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using FluentResults;
using OpenSearch.Client;
using DateRangeSubQuery = Bielu.BetterSearch.Abstractions.Query.SubQueries.DateRangeSubQuery;
using LongRangeSubQuery = Bielu.BetterSearch.Abstractions.Query.SubQueries.LongRangeSubQuery;
using OsDateRangeQuery = OpenSearch.Client.DateRangeQuery;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services.Queries;

internal sealed class NumericRangeSubQueryTranslator : OpenSearchSubQueryTranslator<NumericRangeSubQuery>
{
    protected override Task<Result<QueryContainer>> TranslateAsync(NumericRangeSubQuery query, IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators)
    {
        var field = OpenSearchFieldNames.Require(query.Field, nameof(NumericRangeSubQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<QueryContainer>());
        }

        var bounds = RangeBounds.Require(nameof(NumericRangeSubQuery), query.MinValue, query.MaxValue);
        if (bounds.IsFailed)
        {
            return Task.FromResult(bounds.ToResult<QueryContainer>());
        }

        var finite = RangeBounds.RequireFinite(nameof(NumericRangeSubQuery), query.MinValue, query.MaxValue);
        if (finite.IsFailed)
        {
            return Task.FromResult(finite.ToResult<QueryContainer>());
        }

        return Task.FromResult(Result.Ok<QueryContainer>(new NumericRangeQuery
        {
            Field = field.Value,
            GreaterThanOrEqualTo = query.IncludeMinEdge ? query.MinValue : null,
            GreaterThan = query.IncludeMinEdge ? null : query.MinValue,
            LessThanOrEqualTo = query.IncludeMaxEdge ? query.MaxValue : null,
            LessThan = query.IncludeMaxEdge ? null : query.MaxValue,
            Boost = OpenSearchFieldNames.Boost(query.BoostValue),
            IsVerbatim = true
        }));
    }
}

/// <summary>A <c>long</c> range, so values beyond the precision of a double are sent unchanged.</summary>
internal sealed class LongRangeSubQueryTranslator : OpenSearchSubQueryTranslator<LongRangeSubQuery>
{
    protected override Task<Result<QueryContainer>> TranslateAsync(LongRangeSubQuery query, IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators)
    {
        var field = OpenSearchFieldNames.Require(query.Field, nameof(LongRangeSubQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<QueryContainer>());
        }

        var bounds = RangeBounds.Require(nameof(LongRangeSubQuery), query.MinValue, query.MaxValue);
        if (bounds.IsFailed)
        {
            return Task.FromResult(bounds.ToResult<QueryContainer>());
        }

        return Task.FromResult(Result.Ok<QueryContainer>(new LongRangeQuery
        {
            Field = field.Value,
            GreaterThanOrEqualTo = query.IncludeMinEdge ? query.MinValue : null,
            GreaterThan = query.IncludeMinEdge ? null : query.MinValue,
            LessThanOrEqualTo = query.IncludeMaxEdge ? query.MaxValue : null,
            LessThan = query.IncludeMaxEdge ? null : query.MaxValue,
            Boost = OpenSearchFieldNames.Boost(query.BoostValue),
            IsVerbatim = true
        }));
    }
}

internal sealed class DateRangeSubQueryTranslator : OpenSearchSubQueryTranslator<DateRangeSubQuery>
{
    protected override Task<Result<QueryContainer>> TranslateAsync(DateRangeSubQuery query, IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators)
    {
        var field = OpenSearchFieldNames.Require(query.Field, nameof(DateRangeSubQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<QueryContainer>());
        }

        var bounds = RangeBounds.Require(nameof(DateRangeSubQuery), query.MinValue, query.MaxValue);
        if (bounds.IsFailed)
        {
            return Task.FromResult(bounds.ToResult<QueryContainer>());
        }

        // Sent in UTC, so an unspecified DateTime is read as UTC and a DateTimeOffset keeps its instant.
        var min = query.MinValue is { } minValue ? DateMath.Anchored(minValue.UtcDateTime) : null;
        var max = query.MaxValue is { } maxValue ? DateMath.Anchored(maxValue.UtcDateTime) : null;
        return Task.FromResult(Result.Ok<QueryContainer>(new OsDateRangeQuery
        {
            Field = field.Value,
            GreaterThanOrEqualTo = query.IncludeMinEdge ? min : null,
            GreaterThan = query.IncludeMinEdge ? null : min,
            LessThanOrEqualTo = query.IncludeMaxEdge ? max : null,
            LessThan = query.IncludeMaxEdge ? null : max,
            Boost = OpenSearchFieldNames.Boost(query.BoostValue),
            IsVerbatim = true
        }));
    }
}

/// <summary>A lexicographic range over the terms of the field; use a keyword field for whole values.</summary>
internal sealed class StringRangeSubQueryTranslator : OpenSearchSubQueryTranslator<StringRangeSubQuery>
{
    protected override Task<Result<QueryContainer>> TranslateAsync(StringRangeSubQuery query, IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators)
    {
        var field = OpenSearchFieldNames.Require(query.Field, nameof(StringRangeSubQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<QueryContainer>());
        }

        var bounds = RangeBounds.Require(nameof(StringRangeSubQuery), query.MinValue, query.MaxValue);
        if (bounds.IsFailed)
        {
            return Task.FromResult(bounds.ToResult<QueryContainer>());
        }

        return Task.FromResult(Result.Ok<QueryContainer>(new TermRangeQuery
        {
            Field = field.Value,
            GreaterThanOrEqualTo = query.IncludeMinEdge ? query.MinValue : null,
            GreaterThan = query.IncludeMinEdge ? null : query.MinValue,
            LessThanOrEqualTo = query.IncludeMaxEdge ? query.MaxValue : null,
            LessThan = query.IncludeMaxEdge ? null : query.MaxValue,
            Boost = OpenSearchFieldNames.Boost(query.BoostValue),
            IsVerbatim = true
        }));
    }
}
