using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using OpenSearch.Client;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services.Queries;

/// <summary>
/// A <c>geo_distance</c> query. The field must be mapped as <c>geo_point</c> in the index; dynamic
/// mapping does not create one. The unit is an OpenSearch distance unit, kilometres by default.
/// </summary>
internal sealed class SpatialSearchSubQueryTranslator : OpenSearchSubQueryTranslator<SpatialSearchQuery>
{
    private const string DefaultUnit = "km";

    protected override Task<Result<QueryContainer>> TranslateAsync(SpatialSearchQuery query, IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators)
    {
        var field = OpenSearchFieldNames.Require(query.Field, nameof(SpatialSearchQuery));
        if (field.IsFailed)
        {
            return Task.FromResult(field.ToResult<QueryContainer>());
        }

        if (query.Distance <= 0)
        {
            return Task.FromResult(Result.Fail<QueryContainer>($"{nameof(SpatialSearchQuery)} needs a Distance greater than 0."));
        }

        // OpenSearch.Client does not check coordinates, so an out-of-range one would only fail on the server.
        if (query.Lat is < -90 or > 90 || double.IsNaN(query.Lat))
        {
            return Task.FromResult(Result.Fail<QueryContainer>($"{nameof(SpatialSearchQuery)} has an invalid Lat {query.Lat}: it must be between -90 and 90."));
        }

        if (query.Lng is < -180 or > 180 || double.IsNaN(query.Lng))
        {
            return Task.FromResult(Result.Fail<QueryContainer>($"{nameof(SpatialSearchQuery)} has an invalid Lng {query.Lng}: it must be between -180 and 180."));
        }

        var unit = string.IsNullOrWhiteSpace(query.UnitOfDistance) ? DefaultUnit : query.UnitOfDistance;
        Distance distance;
        try
        {
            // Fixed-point, because a value such as 0.00001 would otherwise format as "1E-05".
            distance = new Distance(query.Distance.ToString("0.###############", System.Globalization.CultureInfo.InvariantCulture) + unit);
        }
        catch (Exception e) when (e is ArgumentException or InvalidCastException)
        {
            return Task.FromResult(Result.Fail<QueryContainer>($"{nameof(SpatialSearchQuery)} has an invalid UnitOfDistance {unit}: {e.Message}"));
        }

        return Task.FromResult(Result.Ok<QueryContainer>(new GeoDistanceQuery
        {
            Field = field.Value,
            Distance = distance,
            Location = new GeoLocation(query.Lat, query.Lng),
            Boost = OpenSearchFieldNames.Boost(query.BoostValue),
            IsVerbatim = true
        }));
    }
}
