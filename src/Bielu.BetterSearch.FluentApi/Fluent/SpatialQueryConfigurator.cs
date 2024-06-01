using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class SpatialQueryConfigurator
    {
        public SpatialQueryConfigurator()
        {
            _query = new SpatialSearchQuery();
        }

        public SpatialQueryConfigurator Must()
        {
            Occurance = Occurance.MUST;
            return this;
        }
        public SpatialQueryConfigurator MustNot()
        {
            Occurance = Occurance.MUSTNOT;
            return this;
        }
        public SpatialQueryConfigurator Should()
        {
            Occurance = Occurance.SHOULD;
            return this;
        }

        public SpatialQueryConfigurator Filter()
        {
            Occurance = Occurance.FILTER;
            return this;
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
        public Occurance Occurance { get; set; }
    }
}
