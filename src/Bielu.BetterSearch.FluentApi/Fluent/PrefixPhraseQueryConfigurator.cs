using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class PrefixPhraseQueryConfigurator : SubQueryConfigurator<PrefixPhraseQueryConfigurator>
    {
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
