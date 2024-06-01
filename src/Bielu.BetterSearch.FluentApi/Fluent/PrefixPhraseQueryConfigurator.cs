using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class PrefixPhraseQueryConfigurator
    {
        public PrefixPhraseQueryConfigurator Must()
        {
            Query.Occurance = Occurance.MUST;
            return this;
        }
        public PrefixPhraseQueryConfigurator MustNot()
        {
            Query.Occurance = Occurance.MUSTNOT;
            return this;
        }
        public PrefixPhraseQueryConfigurator Should()
        {
            Query.Occurance = Occurance.SHOULD;
            return this;
        }

        public PrefixPhraseQueryConfigurator Filter()
        {
            Query.Occurance = Occurance.FILTER;
            return this;
        }

        public PrefixPhraseQueryConfigurator WithField(string fieldName)
        {
            _query.Field = fieldName;
            return this;
        }
        public PrefixPhraseQueryConfigurator WithValue(object value)
        {
            _query.Value = value;
            return this;
        }

        private PrefixPhraseSubQuery _query = new();
        public ISearchSubQuery Query => _query;
    }
}
