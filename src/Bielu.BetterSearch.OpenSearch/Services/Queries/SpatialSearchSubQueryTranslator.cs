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

        var unit = string.IsNullOrWhiteSpace(query.UnitOfDistance) ? DefaultUnit : query.UnitOfDistance;
        Distance distance;
        try
        {
            distance = new Distance(query.Distance.ToString(System.Globalization.CultureInfo.InvariantCulture) + unit);
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
