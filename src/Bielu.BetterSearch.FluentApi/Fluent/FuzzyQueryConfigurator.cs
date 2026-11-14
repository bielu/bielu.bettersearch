using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class FuzzyQueryConfigurator : SubQueryConfigurator<FuzzyQueryConfigurator>
    {
        public FuzzyQueryConfigurator()
        {
            _query = new FuzzyQuery();
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
