using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class TermsQueryConfigurator : SubQueryConfigurator<TermsQueryConfigurator>
    {
        public TermsQueryConfigurator()
        {
            _query = new TermsSubQuery();
        }

        public TermsQueryConfigurator WithField(string fieldName)
        {
            _query.Field = fieldName;
            return this;
        }
        public TermsQueryConfigurator WithValue(params object[] value)
        {
            _query.Value = value.ToList();
            return this;
        }

        private TermsSubQuery _query;
        public ISearchSubQuery Query => _query;
        protected override ISearchSubQuery SubQuery => _query;
    }
}
