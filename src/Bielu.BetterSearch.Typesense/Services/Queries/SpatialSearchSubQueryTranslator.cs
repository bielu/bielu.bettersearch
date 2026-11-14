using System.Globalization;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Typesense.Configuration;
using FluentResults;
using Microsoft.Extensions.Options;
using N = Bielu.BetterSearch.Typesense.Services.TypesenseFieldNames;

namespace Bielu.BetterSearch.Typesense.Services.Queries;

/// <summary>
/// A geo radius filter on the <c>geopoint</c> the provider stores for <c>TypesenseOptions.GeoField</c>, so the query's
/// field must be that one. The unit is an Elasticsearch distance unit, kilometres by default.
/// </summary>
internal sealed class SpatialSearchSubQueryTranslator(IOptionsMonitor<TypesenseOptions> optionsMonitor)
    : TypesenseSubQueryTranslator<SpatialSearchQuery>
{
    private const string DefaultUnit = "km";

    private const string NotNegatable = "Typesense filters cannot negate a geo radius, so a SpatialSearchQuery can't be a MUSTNOT clause.";

    /// <summary>Metres in one of each unit, by every name Elasticsearch accepts.</summary>
    private static readonly Dictionary<string, double> MetresPerUnit = new(StringComparer.Ordinal)
    {
        ["mi"] = 1609.344,
        ["miles"] = 1609.344,
        ["yd"] = 0.9144,
        ["yards"] = 0.9144,
        ["ft"] = 0.3048,
        ["feet"] = 0.3048,
        ["in"] = 0.0254,
        ["inch"] = 0.0254,
        ["km"] = 1000,
        ["kilometers"] = 1000,
        ["m"] = 1,
        ["meters"] = 1,
        ["cm"] = 0.01,
        ["centimeters"] = 0.01,
        ["mm"] = 0.001,
        ["millimeters"] = 0.001,
        ["NM"] = 1852,
        ["nmi"] = 1852,
        ["nauticalmiles"] = 1852,
    };

    protected override Result<TypesenseFilter> Translate(SpatialSearchQuery query, IEnumerable<ISubQueryTranslator<TypesenseFilter>> subQueryTranslators)
    {
        var field = N.Require(query.Field, nameof(SpatialSearchQuery));
        if (field.IsFailed)
        {
            return field.ToResult<TypesenseFilter>();
        }

        var geoField = optionsMonitor.CurrentValue.GeoField;
        if (string.IsNullOrWhiteSpace(geoField))
        {
            return Result.Fail<TypesenseFilter>(
                $"{nameof(SpatialSearchQuery)} needs TypesenseOptions.GeoField: Typesense filters by distance only on a geopoint field, which the provider fills from that field.");
        }

        if (N.Name(field.Value) != geoField)
        {
            return Result.Fail<TypesenseFilter>(
                $"{nameof(SpatialSearchQuery)} field '{field.Value}' is not TypesenseOptions.GeoField '{geoField}': the provider stores a point for that field only.");
        }

        if (query.Distance <= 0 || double.IsNaN(query.Distance) || double.IsInfinity(query.Distance))
        {
            return Result.Fail<TypesenseFilter>($"{nameof(SpatialSearchQuery)} needs a Distance greater than 0.");
        }

        if (query.Lat is < -90 or > 90 || double.IsNaN(query.Lat))
        {
            return Result.Fail<TypesenseFilter>($"{nameof(SpatialSearchQuery)} has an invalid Lat {query.Lat}: it must be between -90 and 90.");
        }

        if (query.Lng is < -180 or > 180 || double.IsNaN(query.Lng))
        {
            return Result.Fail<TypesenseFilter>($"{nameof(SpatialSearchQuery)} has an invalid Lng {query.Lng}: it must be between -180 and 180.");
        }

        var unit = string.IsNullOrWhiteSpace(query.UnitOfDistance) ? DefaultUnit : query.UnitOfDistance;
        if (!MetresPerUnit.TryGetValue(unit, out var metres))
        {
            return Result.Fail<TypesenseFilter>($"{nameof(SpatialSearchQuery)} has an invalid UnitOfDistance {unit}.");
        }

        var typesenseField = N.Geo + geoField;
        var kilometres = query.Distance * metres / 1000;
        return Result.Ok<TypesenseFilter>(new TypesenseFilter.Comparison(typesenseField,
            string.Create(CultureInfo.InvariantCulture, $"{typesenseField}:({query.Lat:R}, {query.Lng:R}, {kilometres.ToString("0.############", CultureInfo.InvariantCulture)} km)"),
            null, NotNegatable));
    }
}
