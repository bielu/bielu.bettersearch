namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    /// <summary>Matches the geo points in <see cref="Field"/> within <see cref="Distance"/> of a centre.</summary>
    public class SpatialSearchSubQuery : ISearchSubQuery
    {
        /// <inheritdoc />
        public Occurrence Occurrence { get; set; } = Occurrence.Must;
        /// <summary>The field that holds the geo point.</summary>
        public string? Field { get; set; }
        /// <inheritdoc />
        public double BoostValue { get; set; } = 1;
        /// <summary>The unit of <see cref="Distance"/>, kilometres by default.</summary>
        public DistanceUnit UnitOfDistance { get; set; } = DistanceUnit.Kilometers;
        /// <summary>The radius around the centre, in <see cref="UnitOfDistance"/>; it must be greater than 0.</summary>
        public double Distance { get; set; }
        /// <summary>The latitude of the centre.</summary>
        public double Lat { get; set; }
        /// <summary>The longitude of the centre.</summary>
        public double Lng { get; set; }
    }
}
