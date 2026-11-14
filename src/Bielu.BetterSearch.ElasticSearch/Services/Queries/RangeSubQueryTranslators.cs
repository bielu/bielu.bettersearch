using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FluentResults;
using DateRangeQuery = Bielu.BetterSearch.Abstractions.Query.SubQueries.DateRangeQuery;
using EsDateRangeQuery = Elastic.Clients.Elasticsearch.QueryDsl.DateRangeQuery;
using EsQuery = Elastic.Clients.Elasticsearch.QueryDsl.Query;

namespace Bielu.BetterSearch.ElasticSearch.Services.Queries;

internal sealed class NumericRangeSubQueryTranslator : ElasticSearchSubQueryTranslator<NumericRange>
{
    protected override Task<Result<EsQuery>> TranslateAsync(NumericRange query, IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators)
    {
        var field = ElasticSearchFieldNames.Require(query.Field, nameof(NumericRange));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<EsQuery>());
        }

        var bounds = RangeBounds.Require(nameof(NumericRange), query.MinValue, query.MaxValue);
        if (bounds.IsFailed)
        {
            return Task.FromResult(bounds.ToResult<EsQuery>());
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
internal sealed class LongRangeSubQueryTranslator : ElasticSearchSubQueryTranslator<LongRange>
{
    protected override Task<Result<EsQuery>> TranslateAsync(LongRange query, IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators)
    {
        var field = ElasticSearchFieldNames.Require(query.Field, nameof(LongRange));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<EsQuery>());
        }

        var bounds = RangeBounds.Require(nameof(LongRange), query.MinValue, query.MaxValue);
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

internal sealed class DateRangeSubQueryTranslator : ElasticSearchSubQueryTranslator<DateRangeQuery>
{
    protected override Task<Result<EsQuery>> TranslateAsync(DateRangeQuery query, IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators)
    {
        var field = ElasticSearchFieldNames.Require(query.Field, nameof(DateRangeQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<EsQuery>());
        }

        var bounds = RangeBounds.Require(nameof(DateRangeQuery), query.MinValue, query.MaxValue);
        if (bounds.IsFailed)
        {
            return Task.FromResult(bounds.ToResult<EsQuery>());
        }

        return Task.FromResult(Result.Ok<EsQuery>(new EsDateRangeQuery
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

/// <summary>A lexicographic range over the terms of the field; use a keyword field for whole values.</summary>
internal sealed class StringRangeSubQueryTranslator : ElasticSearchSubQueryTranslator<StringRange>
{
    protected override Task<Result<EsQuery>> TranslateAsync(StringRange query, IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators)
    {
        var field = ElasticSearchFieldNames.Require(query.Field, nameof(StringRange));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<EsQuery>());
        }

        var bounds = RangeBounds.Require(nameof(StringRange), query.MinValue, query.MaxValue);
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
