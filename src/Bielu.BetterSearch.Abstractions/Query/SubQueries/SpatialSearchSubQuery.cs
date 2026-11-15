namespace Bielu.BetterSearch.Abstractions.Query.SubQueries
{
    public class SpatialSearchSubQuery : ISearchSubQuery
    {
        public Occurrence Occurrence { get; set; } = Occurrence.Must;
        public string? Field { get; set; }
        public int BoostValue { get; set; } = 1;
        public string? UnitOfDistance { get; set; }
        public double Distance { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}
