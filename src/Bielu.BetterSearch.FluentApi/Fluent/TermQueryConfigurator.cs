using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    /// <summary>Configures a <see cref="TermSubQuery"/>.</summary>
    public class TermQueryConfigurator : SubQueryConfigurator<TermQueryConfigurator>
    {
        /// <summary>Creates a configurator for a new <see cref="TermSubQuery"/>.</summary>
        public TermQueryConfigurator()
        {
            _query = new TermSubQuery();
        }

        /// <summary>Sets the <see cref="TermSubQuery.Field"/> the sub-query matches on.</summary>
        /// <param name="fieldName">The field name.</param>
        /// <returns>This configurator, for chaining.</returns>
        public TermQueryConfigurator WithField(string fieldName)
        {
            _query.Field = fieldName;
            return this;
        }
        /// <summary>Sets the <see cref="TermSubQuery.Value"/> to match.</summary>
        /// <param name="value">The exact value to match.</param>
        /// <returns>This configurator, for chaining.</returns>
        public TermQueryConfigurator WithValue(object value)
        {
            _query.Value = value;
            return this;
        }

        private TermSubQuery _query;
        /// <summary>The sub-query this configurator builds.</summary>
        public ISearchSubQuery Query => _query;
        /// <inheritdoc />
        protected override ISearchSubQuery SubQuery => _query;
    }
}
