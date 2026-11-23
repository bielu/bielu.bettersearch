namespace Bielu.BetterSearch.Abstractions.Query.SubQueries;

/// <summary>The unit of <see cref="SpatialSearchSubQuery.Distance"/>: the distance units Elasticsearch accepts.</summary>
public enum DistanceUnit
{
    /// <summary>Kilometres.</summary>
    Kilometers = 0,
    /// <summary>Metres.</summary>
    Meters = 1,
    /// <summary>Centimetres.</summary>
    Centimeters = 2,
    /// <summary>Millimetres.</summary>
    Millimeters = 3,
    /// <summary>Miles.</summary>
    Miles = 4,
    /// <summary>Yards.</summary>
    Yards = 5,
    /// <summary>Feet.</summary>
    Feet = 6,
    /// <summary>Inches.</summary>
    Inches = 7,
    /// <summary>Nautical miles.</summary>
    NauticalMiles = 8
}
