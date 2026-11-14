using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class TermQueryConfigurator : SubQueryConfigurator<TermQueryConfigurator>
    {
        public TermQueryConfigurator()
        {
            _query = new TermSubQuery();
        }

        public TermQueryConfigurator WithField(string fieldName)
        {
            _query.Field = fieldName;
            return this;
        }
        public TermQueryConfigurator WithValue(object value)
        {
            _query.Value = value;
            return this;
        }

        private TermSubQuery _query;
        public ISearchSubQuery Query => _query;
        protected override ISearchSubQuery SubQuery => _query;
    }
}
