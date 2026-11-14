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
        public SpatialQueryConfigurator WithUnitOfDistance(string value)
        {
            _query.UnitOfDistance = value;
            return this;
        }

        private SpatialSearchQuery _query;
        public ISearchSubQuery Query => _query;
    }
}
