namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    public class SpatialSearchSubQuery : ISearchSubQuery
    {
        public Occurrence Occurrence { get; set; } = Occurrence.Must;
        public string? Field { get; set; }
        public double BoostValue { get; set; } = 1;
        /// <summary>The unit of <see cref="Distance"/>, kilometres by default.</summary>
        public DistanceUnit UnitOfDistance { get; set; } = DistanceUnit.Kilometers;
        public double Distance { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}
