using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.Meilisearch.Services.Queries;

internal sealed class NumericRangeSubQueryTranslator : MeilisearchSubQueryTranslator<NumericRange>
{
    protected override Result<MeilisearchFilter> Translate(NumericRange query, IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators) =>
        RangeFilter.Create(query, nameof(NumericRange), MeilisearchFieldNames.Fields,
            MeilisearchFieldNames.Number(query.MinValue), MeilisearchFieldNames.Number(query.MaxValue));
}

/// <summary>Meilisearch stores numbers as doubles, so values beyond 2^53 compare at that precision.</summary>
internal sealed class LongRangeSubQueryTranslator : MeilisearchSubQueryTranslator<LongRange>
{
    protected override Result<MeilisearchFilter> Translate(LongRange query, IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators) =>
        RangeFilter.Create(query, nameof(LongRange), MeilisearchFieldNames.Fields,
            MeilisearchFieldNames.Number(query.MinValue), MeilisearchFieldNames.Number(query.MaxValue));
}

/// <summary>Compares the dates the provider stores as Unix milliseconds; a date without a time zone is UTC.</summary>
internal sealed class DateRangeSubQueryTranslator : MeilisearchSubQueryTranslator<DateRangeQuery>
{
    protected override Result<MeilisearchFilter> Translate(DateRangeQuery query, IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators) =>
        RangeFilter.Create(query, nameof(DateRangeQuery), MeilisearchFieldNames.Dates,
            MeilisearchFieldNames.Number(MeilisearchDocument.ToUnixMilliseconds(query.MinValue)),
            MeilisearchFieldNames.Number(MeilisearchDocument.ToUnixMilliseconds(query.MaxValue)));
}

/// <summary>Compares the field's lower-cased words, as Elasticsearch compares analyzed tokens; a <c>fields.</c> name compares whole values.</summary>
internal sealed class StringRangeSubQueryTranslator : MeilisearchSubQueryTranslator<StringRange>
{
    protected override Result<MeilisearchFilter> Translate(StringRange query, IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators)
    {
        var storedValue = query.Field != null && MeilisearchFieldNames.IsStoredValue(query.Field);
        string? Bound(string? value) => value == null ? null : MeilisearchFieldNames.Quote(storedValue ? value : value.ToLowerInvariant());
        return RangeFilter.Create(query, nameof(StringRange), MeilisearchFieldNames.Tokens, Bound(query.MinValue), Bound(query.MaxValue));
    }
}

internal static class RangeFilter
{
    public static Result<MeilisearchFilter> Create(BaseRangeQuery query, string queryName, string map, string? min, string? max)
    {
        var field = MeilisearchFieldNames.Require(query.Field, queryName);
        return field.IsFailed
            ? field.ToResult<MeilisearchFilter>()
            : Result.Ok(new MeilisearchFilter(MeilisearchFieldNames.Range(
                MeilisearchFieldNames.In(map, field.Value), min, query.IncludeMinEdge, max, query.IncludeMaxEdge)));
    }
}
