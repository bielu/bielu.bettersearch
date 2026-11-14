using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class SpatialQueryConfigurator : SubQueryConfigurator<SpatialQueryConfigurator>
    {
        public SpatialQueryConfigurator()
        {
            _query = new SpatialSearchQuery();
        }

        public SpatialQueryConfigurator WithField(string fieldName)
        {
            _query.Field = fieldName;
            return this;
        }
        public SpatialQueryConfigurator WithDistance(double value)
        {
            _query.Distance = value;
            return this;
        }
        /// <summary>Sets the centre the distance is measured from, as latitude and longitude in degrees.</summary>
        public SpatialQueryConfigurator WithCenter(double lat, double lng)
        {
            _query.Lat = lat;
            _query.Lng = lng;
            return this;
        }
        public SpatialQueryConfigurator WithUnitOfDistance(string value)
        {
            _query.UnitOfDistance = value;
            return this;
        }

        private SpatialSearchQuery _query;
        public ISearchSubQuery Query => _query;
        protected override ISearchSubQuery SubQuery => _query;
    }
}
