using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class PrefixQueryConfigurator
    {
        public PrefixQueryConfigurator Must()
            {
                Query.Occurance = Occurance.MUST;
                return this;
            }
            public PrefixQueryConfigurator MustNot()
            {
                Query.Occurance = Occurance.MUSTNOT;
                return this;
            }
            public PrefixQueryConfigurator Should()
            {
                Query.Occurance = Occurance.SHOULD;
                return this;
            }

            public PrefixQueryConfigurator Filter()
            {
                Query.Occurance = Occurance.FILTER;
                return this;
            }

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
