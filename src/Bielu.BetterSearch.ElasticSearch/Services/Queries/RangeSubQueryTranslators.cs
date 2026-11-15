using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FluentResults;
using DateRangeSubQuery = Bielu.BetterSearch.Abstractions.Query.SubQueries.DateRangeSubQuery;
using EsDateRangeQuery = Elastic.Clients.Elasticsearch.QueryDsl.DateRangeQuery;
using EsQuery = Elastic.Clients.Elasticsearch.QueryDsl.Query;

namespace Bielu.BetterSearch.ElasticSearch.Services.Queries;

internal sealed class NumericRangeSubQueryTranslator : ElasticSearchSubQueryTranslator<NumericRangeSubQuery>
{
    protected override Task<Result<EsQuery>> TranslateAsync(NumericRangeSubQuery query, IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators)
    {
        var field = ElasticSearchFieldNames.Require(query.Field, nameof(NumericRangeSubQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<EsQuery>());
        }

        var bounds = RangeBounds.Require(nameof(NumericRangeSubQuery), query.MinValue, query.MaxValue);
        if (bounds.IsFailed)
        {
            return Task.FromResult(bounds.ToResult<EsQuery>());
        }

        var finite = RangeBounds.RequireFinite(nameof(NumericRangeSubQuery), query.MinValue, query.MaxValue);
        if (finite.IsFailed)
        {
            return Task.FromResult(finite.ToResult<EsQuery>());
        }

        return Task.FromResult(Result.Ok<EsQuery>(new NumberRangeQuery
        {
            Field = field.Value,
            Gte = query.IncludeMinEdge ? query.MinValue : null,
            Gt = query.IncludeMinEdge ? null : query.MinValue,
            Lte = query.IncludeMaxEdge ? query.MaxValue : null,
            Lt = query.IncludeMaxEdge ? null : query.MaxValue,
            Boost = ElasticSearchFieldNames.Boost(query.BoostValue)
        }));
    }
}

/// <summary>An untyped range, so values beyond the precision of a double are sent unchanged.</summary>
internal sealed class LongRangeSubQueryTranslator : ElasticSearchSubQueryTranslator<LongRangeSubQuery>
{
    protected override Task<Result<EsQuery>> TranslateAsync(LongRangeSubQuery query, IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators)
    {
        var field = ElasticSearchFieldNames.Require(query.Field, nameof(LongRangeSubQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<EsQuery>());
        }

        var bounds = RangeBounds.Require(nameof(LongRangeSubQuery), query.MinValue, query.MaxValue);
        if (bounds.IsFailed)
        {
            return Task.FromResult(bounds.ToResult<EsQuery>());
        }

        return Task.FromResult(Result.Ok<EsQuery>(new UntypedRangeQuery
        {
            Field = field.Value,
            Gte = query.IncludeMinEdge ? query.MinValue : null,
            Gt = query.IncludeMinEdge ? null : query.MinValue,
            Lte = query.IncludeMaxEdge ? query.MaxValue : null,
            Lt = query.IncludeMaxEdge ? null : query.MaxValue,
            Boost = ElasticSearchFieldNames.Boost(query.BoostValue)
        }));
    }
}

internal sealed class DateRangeSubQueryTranslator : ElasticSearchSubQueryTranslator<DateRangeSubQuery>
{
    protected override Task<Result<EsQuery>> TranslateAsync(DateRangeSubQuery query, IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators)
    {
        var field = ElasticSearchFieldNames.Require(query.Field, nameof(DateRangeSubQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<EsQuery>());
        }

        var bounds = RangeBounds.Require(nameof(DateRangeSubQuery), query.MinValue, query.MaxValue);
        if (bounds.IsFailed)
        {
            return Task.FromResult(bounds.ToResult<EsQuery>());
        }

        // Sent in UTC, so an unspecified DateTime is read as UTC and a DateTimeOffset keeps its instant.
        var min = query.MinValue?.UtcDateTime;
        var max = query.MaxValue?.UtcDateTime;
        return Task.FromResult(Result.Ok<EsQuery>(new EsDateRangeQuery
        {
            Field = field.Value,
            Gte = query.IncludeMinEdge ? min : null,
            Gt = query.IncludeMinEdge ? null : min,
            Lte = query.IncludeMaxEdge ? max : null,
            Lt = query.IncludeMaxEdge ? null : max,
            Boost = ElasticSearchFieldNames.Boost(query.BoostValue)
        }));
    }
}

/// <summary>A lexicographic range over the terms of the field; use a keyword field for whole values.</summary>
internal sealed class StringRangeSubQueryTranslator : ElasticSearchSubQueryTranslator<StringRangeSubQuery>
{
    protected override Task<Result<EsQuery>> TranslateAsync(StringRangeSubQuery query, IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators)
    {
        var field = ElasticSearchFieldNames.Require(query.Field, nameof(StringRangeSubQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<EsQuery>());
        }

        var bounds = RangeBounds.Require(nameof(StringRangeSubQuery), query.MinValue, query.MaxValue);
        if (bounds.IsFailed)
        {
            return Task.FromResult(bounds.ToResult<EsQuery>());
        }

        return Task.FromResult(Result.Ok<EsQuery>(new TermRangeQuery
        {
            Field = field.Value,
            Gte = query.IncludeMinEdge ? query.MinValue : null,
            Gt = query.IncludeMinEdge ? null : query.MinValue,
            Lte = query.IncludeMaxEdge ? query.MaxValue : null,
            Lt = query.IncludeMaxEdge ? null : query.MaxValue,
            Boost = ElasticSearchFieldNames.Boost(query.BoostValue)
        }));
    }
}
