using System.Globalization;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FluentResults;
using EsQuery = Elastic.Clients.Elasticsearch.QueryDsl.Query;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.ElasticSearch.Services.Queries;

/// <summary>
/// A <c>geo_distance</c> query. The field must be mapped as <c>geo_point</c> in the index; dynamic
/// mapping does not create one. The unit is an Elasticsearch distance unit, kilometres by default.
/// </summary>
internal sealed class SpatialSearchSubQueryTranslator : ElasticSearchSubQueryTranslator<SpatialSearchQuery>
{
    private const string DefaultUnit = "km";

    protected override Task<Result<EsQuery>> TranslateAsync(SpatialSearchQuery query, IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators)
    {
        var field = ElasticSearchFieldNames.Require(query.Field, nameof(SpatialSearchQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<EsQuery>());
        }

        if (query.Distance <= 0)
        {
            return Task.FromResult(Result.Fail<EsQuery>($"{nameof(SpatialSearchQuery)} needs a Distance greater than 0."));
        }

        var unit = string.IsNullOrWhiteSpace(query.UnitOfDistance) ? DefaultUnit : query.UnitOfDistance;
        return Task.FromResult(Result.Ok<EsQuery>(new GeoDistanceQuery
        {
            Field = field.Value,
            Distance = query.Distance.ToString(CultureInfo.InvariantCulture) + unit,
            Location = GeoLocation.LatitudeLongitude(new LatLonGeoLocation { Lat = query.Lat, Lon = query.Lng }),
            Boost = ElasticSearchFieldNames.Boost(query.BoostValue)
        }));
    }
}
