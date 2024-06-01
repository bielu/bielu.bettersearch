using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class FuzzyQueryConfigurator
    {
        public FuzzyQueryConfigurator()
        {
            _query = new FuzzyQuery();
        }

        public FuzzyQueryConfigurator Must()
        {
            Query.Occurance = Occurance.MUST;
            return this;
        }
        public FuzzyQueryConfigurator MustNot()
        {
            Query.Occurance = Occurance.MUSTNOT;
            return this;
        }
        public FuzzyQueryConfigurator Should()
        {
            Query.Occurance = Occurance.SHOULD;
            return this;
        }

        public FuzzyQueryConfigurator Filter()
        {
            Query.Occurance = Occurance.FILTER;
            return this;
        }

        public FuzzyQueryConfigurator WithField(string fieldName)
        {
            _query.Field = fieldName;
            return this;
        }
        public FuzzyQueryConfigurator WithValue(object value)
        {
            _query.Value = value;
            return this;
        }

        private FuzzyQuery _query;
        public ISearchSubQuery Query => _query;
    }
}
