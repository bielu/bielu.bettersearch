using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class PrefixQueryConfigurator : SubQueryConfigurator<PrefixQueryConfigurator>
    {
        public PrefixQueryConfigurator WithField(string fieldName)
        {
            _query.Field = fieldName;
            return this;
        }
        public PrefixQueryConfigurator WithValue(object value)
        {
            _query.Value = value;
            return this;
        }

        private PrefixSubQuery _query = new();
        public ISearchSubQuery Query => _query;
    }
}
