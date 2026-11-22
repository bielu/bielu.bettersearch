using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Services;
using FluentResults;
using NpgsqlTypes;

namespace Bielu.BetterSearch.PostgreSql.Services.Queries;

/// <summary>
/// Matches when a point of the field is within the distance of the query's point, by great-circle (haversine)
/// distance on a sphere with Elasticsearch's mean Earth radius, with no extension needed. A point is a
/// <c>"lat,lon"</c> string, as Elasticsearch accepts for a <c>geo_point</c>, or an object with <c>lat</c> and
/// <c>lon</c> (or <c>lng</c>). The unit is an Elasticsearch distance unit, kilometres by default. No index serves
/// the distance, so every document holding the field is checked.
/// </summary>
internal sealed class SpatialSearchSubQueryTranslator : PostgreSqlSubQueryTranslator<SpatialSearchSubQuery>
{
    /// <summary>Elasticsearch's mean Earth radius, in metres.</summary>
    internal const double EarthRadius = 6_371_008.7714;

    protected override Result<PostgreSqlFilter> Translate(SpatialSearchSubQuery query)
    {
        var field = RequireField(query.Field, nameof(SpatialSearchSubQuery));
        if (field.IsFailed)
        {
            return field.ToResult<PostgreSqlFilter>();
        }

        if (query.Distance <= 0 || !double.IsFinite(query.Distance))
        {
            return Result.Fail<PostgreSqlFilter>($"{nameof(SpatialSearchSubQuery)} needs a Distance greater than 0.");
        }

        if (query.Lat is < -90 or > 90 || double.IsNaN(query.Lat))
        {
            return Result.Fail<PostgreSqlFilter>($"{nameof(SpatialSearchSubQuery)} has an invalid Lat {query.Lat}: it must be between -90 and 90.");
        }

        if (query.Lng is < -180 or > 180 || double.IsNaN(query.Lng))
        {
            return Result.Fail<PostgreSqlFilter>($"{nameof(SpatialSearchSubQuery)} has an invalid Lng {query.Lng}: it must be between -180 and 180.");
        }

        var unit = DistanceUnits.Resolve(query.UnitOfDistance);
        if (unit.IsFailed)
        {
            return unit.ToResult<PostgreSqlFilter>();
        }

        var metresPerUnit = unit.Value.Metres;

        var name = field.Value;
        var metres = query.Distance * metresPerUnit;
        return Result.Ok<PostgreSqlFilter>(new PostgreSqlFilter.Condition($"{nameof(SpatialSearchSubQuery)} on {name}", p =>
        {
            var lat = p.Add(query.Lat, NpgsqlDbType.Double);
            var lng = p.Add(query.Lng, NpgsqlDbType.Double);
            var radius = p.Add(EarthRadius, NpgsqlDbType.Double);
            var distance = p.Add(metres, NpgsqlDbType.Double);
            return "EXISTS (SELECT 1 FROM (SELECT " +
                   "trim(CASE jsonb_typeof(e) WHEN 'object' THEN e ->> 'lat' WHEN 'string' THEN split_part(e #>> '{}', ',', 1) END) AS lat, " +
                   "trim(CASE jsonb_typeof(e) WHEN 'object' THEN coalesce(e ->> 'lon', e ->> 'lng') WHEN 'string' THEN split_part(e #>> '{}', ',', 2) END) AS lon " +
                   $"FROM {p.Elements(name)} e) t, " +
                   "LATERAL (SELECT CASE WHEN pg_input_is_valid(t.lat, 'float8') AND pg_input_is_valid(t.lon, 'float8') THEN t.lat::float8 END AS lat, " +
                   "CASE WHEN pg_input_is_valid(t.lat, 'float8') AND pg_input_is_valid(t.lon, 'float8') THEN t.lon::float8 END AS lon) c " +
                   $"WHERE c.lat IS NOT NULL AND 2 * {radius} * asin(least(1, sqrt(power(sin(radians(c.lat - {lat}) / 2), 2) + " +
                   $"cos(radians({lat})) * cos(radians(c.lat)) * power(sin(radians(c.lon - {lng}) / 2), 2)))) <= {distance})";
        }));
    }
}
