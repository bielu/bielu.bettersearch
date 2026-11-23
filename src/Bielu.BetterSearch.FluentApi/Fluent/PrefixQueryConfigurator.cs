using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    /// <summary>Configures a <see cref="PrefixSubQuery"/>.</summary>
    public class PrefixQueryConfigurator : SubQueryConfigurator<PrefixQueryConfigurator>
    {
        /// <summary>Sets the <see cref="PrefixSubQuery.Field"/> the sub-query matches on.</summary>
        /// <param name="fieldName">The field name.</param>
        /// <returns>This configurator, for chaining.</returns>
        public PrefixQueryConfigurator WithField(string fieldName)
        {
            _query.Field = fieldName;
            return this;
        }
        /// <summary>Sets the <see cref="PrefixSubQuery.Value"/> to match.</summary>
        /// <param name="value">The prefix to match.</param>
        /// <returns>This configurator, for chaining.</returns>
        public PrefixQueryConfigurator WithValue(object value)
        {
            _query.Value = value;
            return this;
        }

        private PrefixSubQuery _query = new();
        /// <summary>The sub-query this configurator builds.</summary>
        public ISearchSubQuery Query => _query;
        /// <inheritdoc />
        protected override ISearchSubQuery SubQuery => _query;
    }
}
