using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    /// <summary>Configures a <see cref="PrefixPhraseSubQuery"/>.</summary>
    public class PrefixPhraseQueryConfigurator : SubQueryConfigurator<PrefixPhraseQueryConfigurator>
    {
        /// <summary>Sets the <see cref="PrefixPhraseSubQuery.Field"/> the sub-query matches on.</summary>
        /// <param name="fieldName">The field name.</param>
        /// <returns>This configurator, for chaining.</returns>
        public PrefixPhraseQueryConfigurator WithField(string fieldName)
        {
            _query.Field = fieldName;
            return this;
        }
        /// <summary>Sets the <see cref="PrefixPhraseSubQuery.Value"/> to match.</summary>
        /// <param name="value">The phrase whose last word is matched as a prefix.</param>
        /// <returns>This configurator, for chaining.</returns>
        public PrefixPhraseQueryConfigurator WithValue(object value)
        {
            _query.Value = value;
            return this;
        }

        private PrefixPhraseSubQuery _query = new();
        /// <summary>The sub-query this configurator builds.</summary>
        public ISearchSubQuery Query => _query;
        /// <inheritdoc />
        protected override ISearchSubQuery SubQuery => _query;
    }
}
