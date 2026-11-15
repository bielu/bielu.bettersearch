namespace Bielu.BetterSearch.Abstractions.Query.SubQueries;

/// <summary>The unit of <see cref="SpatialSearchSubQuery.Distance"/>: the distance units Elasticsearch accepts.</summary>
public enum DistanceUnit
{
    Kilometers = 0,
    Meters = 1,
    Centimeters = 2,
    Millimeters = 3,
    Miles = 4,
    Yards = 5,
    Feet = 6,
    Inches = 7,
    NauticalMiles = 8
}
