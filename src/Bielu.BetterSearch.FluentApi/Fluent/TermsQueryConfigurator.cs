using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    /// <summary>Configures a <see cref="TermsSubQuery"/>.</summary>
    public class TermsQueryConfigurator : SubQueryConfigurator<TermsQueryConfigurator>
    {
        /// <summary>Creates a configurator for a new <see cref="TermsSubQuery"/>.</summary>
        public TermsQueryConfigurator()
        {
            _query = new TermsSubQuery();
        }

        /// <summary>Sets the <see cref="TermsSubQuery.Field"/> the sub-query matches on.</summary>
        /// <param name="fieldName">The field name.</param>
        /// <returns>This configurator, for chaining.</returns>
        public TermsQueryConfigurator WithField(string fieldName)
        {
            _query.Field = fieldName;
            return this;
        }
        /// <summary>Sets the <see cref="TermsSubQuery.Value"/> list; a document matches when the field holds any of them.</summary>
        /// <param name="value">The values to match.</param>
        /// <returns>This configurator, for chaining.</returns>
        public TermsQueryConfigurator WithValue(params object[] value)
        {
            _query.Value = value.ToList();
            return this;
        }

        private TermsSubQuery _query;
        /// <summary>The sub-query this configurator builds.</summary>
        public ISearchSubQuery Query => _query;
        /// <inheritdoc />
        protected override ISearchSubQuery SubQuery => _query;
    }
}
