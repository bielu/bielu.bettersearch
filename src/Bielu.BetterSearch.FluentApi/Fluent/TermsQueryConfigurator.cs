using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class TermsQueryConfigurator
    {

        public TermsQueryConfigurator()
        {
            _query = new TermsSubQuery();
        }

        public TermsQueryConfigurator Must()
        {
            Occurance = Occurance.MUST;
            return this;
        }
        public TermsQueryConfigurator MustNot()
        {
            Occurance = Occurance.MUSTNOT;
            return this;
        }
        public TermsQueryConfigurator Should()
        {
            Occurance = Occurance.SHOULD;
            return this;
        }

        public TermsQueryConfigurator Filter()
        {
            Occurance = Occurance.FILTER;
            return this;
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
        public Occurance Occurance { get; set; }
    }
}
