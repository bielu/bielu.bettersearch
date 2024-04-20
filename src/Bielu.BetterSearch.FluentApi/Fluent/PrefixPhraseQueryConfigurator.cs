using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class PrefixPhraseQueryConfigurator
    {
        public PrefixPhraseQueryConfigurator Must()
        {
            Query.Occurance = Occurance.Must;
            return this;
        }
        public PrefixPhraseQueryConfigurator MustNot()
        {
            Query.Occurance = Occurance.MustNot;
            return this;
        }
        public PrefixPhraseQueryConfigurator Should()
        {
            Query.Occurance = Occurance.Should;
            return this;
        }

        public PrefixPhraseQueryConfigurator Filter()
        {
            Query.Occurance = Occurance.Filter;
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
