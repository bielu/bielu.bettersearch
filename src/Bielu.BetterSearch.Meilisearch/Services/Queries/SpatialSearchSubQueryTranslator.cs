using System.Globalization;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Meilisearch.Configuration;
using FluentResults;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.Meilisearch.Services.Queries;

/// <summary>
/// A <c>_geoRadius</c> filter. Meilisearch only filters by distance on its top-level <c>_geo</c> attribute, so the
/// query's field must be <c>MeilisearchOptions.GeoField</c>, which the provider copies there. The unit is an
/// Elasticsearch distance unit, kilometres by default.
/// </summary>
internal sealed class SpatialSearchSubQueryTranslator(IOptionsMonitor<MeilisearchOptions> optionsMonitor)
    : MeilisearchSubQueryTranslator<SpatialSearchQuery>
{
    private const string DefaultUnit = "km";

    /// <summary>Metres in one of each unit, by every name Elasticsearch accepts.</summary>
    private static readonly Dictionary<string, double> MetresPerUnit = new(StringComparer.Ordinal)
    {
        ["mi"] = 1609.344, ["miles"] = 1609.344,
        ["yd"] = 0.9144, ["yards"] = 0.9144,
        ["ft"] = 0.3048, ["feet"] = 0.3048,
        ["in"] = 0.0254, ["inch"] = 0.0254,
        ["km"] = 1000, ["kilometers"] = 1000,
        ["m"] = 1, ["meters"] = 1,
        ["cm"] = 0.01, ["centimeters"] = 0.01,
        ["mm"] = 0.001, ["millimeters"] = 0.001,
        ["NM"] = 1852, ["nmi"] = 1852, ["nauticalmiles"] = 1852,
    };

    protected override Result<MeilisearchFilter> Translate(SpatialSearchQuery query, IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators)
    {
        var field = MeilisearchFieldNames.Require(query.Field, nameof(SpatialSearchQuery));
        if (field.IsFailed)
        {
            return field.ToResult<MeilisearchFilter>();
        }

        var geoField = optionsMonitor.CurrentValue.GeoField;
        if (string.IsNullOrWhiteSpace(geoField))
        {
            return Result.Fail<MeilisearchFilter>(
                $"{nameof(SpatialSearchQuery)} needs MeilisearchOptions.GeoField: Meilisearch only filters by distance on the top-level _geo attribute, which the provider fills from that field.");
        }

        if (field.Value != geoField)
        {
            return Result.Fail<MeilisearchFilter>(
                $"{nameof(SpatialSearchQuery)} field '{field.Value}' is not MeilisearchOptions.GeoField '{geoField}': Meilisearch only filters by distance on one field.");
        }

        if (query.Distance <= 0 || double.IsNaN(query.Distance) || double.IsInfinity(query.Distance))
        {
            return Result.Fail<MeilisearchFilter>($"{nameof(SpatialSearchQuery)} needs a Distance greater than 0.");
        }

        if (query.Lat is < -90 or > 90 || double.IsNaN(query.Lat))
        {
            return Result.Fail<MeilisearchFilter>($"{nameof(SpatialSearchQuery)} has an invalid Lat {query.Lat}: it must be between -90 and 90.");
        }

        if (query.Lng is < -180 or > 180 || double.IsNaN(query.Lng))
        {
            return Result.Fail<MeilisearchFilter>($"{nameof(SpatialSearchQuery)} has an invalid Lng {query.Lng}: it must be between -180 and 180.");
        }

        var unit = string.IsNullOrWhiteSpace(query.UnitOfDistance) ? DefaultUnit : query.UnitOfDistance;
        if (!MetresPerUnit.TryGetValue(unit, out var metres))
        {
            return Result.Fail<MeilisearchFilter>($"{nameof(SpatialSearchQuery)} has an invalid UnitOfDistance {unit}.");
        }

        return Filter(string.Create(CultureInfo.InvariantCulture,
            $"_geoRadius({query.Lat:R}, {query.Lng:R}, {query.Distance * metres:R})"));
    }
}
