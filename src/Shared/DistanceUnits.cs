using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using FluentResults;

namespace Bielu.BetterSearch.Services;

/// <summary>What each <see cref="DistanceUnit"/> means to the providers, so every engine reads a distance the same way.</summary>
internal static class DistanceUnits
{
    /// <summary>
    /// The unit's Elasticsearch and OpenSearch name and its length in metres, or a failed result naming the value
    /// when it is not a defined <see cref="DistanceUnit"/>.
    /// </summary>
    public static Result<(string Name, double Metres)> Resolve(DistanceUnit unit) => unit switch
    {
        DistanceUnit.Kilometers => ("km", 1000),
        DistanceUnit.Meters => ("m", 1),
        DistanceUnit.Centimeters => ("cm", 0.01),
        DistanceUnit.Millimeters => ("mm", 0.001),
        DistanceUnit.Miles => ("mi", 1609.344),
        DistanceUnit.Yards => ("yd", 0.9144),
        DistanceUnit.Feet => ("ft", 0.3048),
        DistanceUnit.Inches => ("in", 0.0254),
        DistanceUnit.NauticalMiles => ("nmi", 1852),
        _ => Result.Fail<(string, double)>($"{nameof(SpatialSearchSubQuery)} has an invalid UnitOfDistance {(int)unit}.")
    };
}
