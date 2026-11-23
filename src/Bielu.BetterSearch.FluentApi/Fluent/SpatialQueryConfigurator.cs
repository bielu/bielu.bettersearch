using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    /// <summary>Configures a <see cref="SpatialSearchSubQuery"/>.</summary>
    public class SpatialQueryConfigurator : SubQueryConfigurator<SpatialQueryConfigurator>
    {
        /// <summary>Creates a configurator for a new <see cref="SpatialSearchSubQuery"/>.</summary>
        public SpatialQueryConfigurator()
        {
            _query = new SpatialSearchSubQuery();
        }

        /// <summary>Sets the <see cref="SpatialSearchSubQuery.Field"/> the sub-query matches on.</summary>
        /// <param name="fieldName">The field name.</param>
        /// <returns>This configurator, for chaining.</returns>
        public SpatialQueryConfigurator WithField(string fieldName)
        {
            _query.Field = fieldName;
            return this;
        }
        /// <summary>Sets the <see cref="SpatialSearchSubQuery.Distance"/> from the centre within which documents match.</summary>
        /// <param name="value">The distance, in the unit set by <see cref="WithUnitOfDistance"/>.</param>
        /// <returns>This configurator, for chaining.</returns>
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
        /// <summary>Sets the unit of the distance, kilometres by default.</summary>
        public SpatialQueryConfigurator WithUnitOfDistance(DistanceUnit value)
        {
            _query.UnitOfDistance = value;
            return this;
        }

        private SpatialSearchSubQuery _query;
        /// <summary>The sub-query this configurator builds.</summary>
        public ISearchSubQuery Query => _query;
        /// <inheritdoc />
        protected override ISearchSubQuery SubQuery => _query;
    }
}
