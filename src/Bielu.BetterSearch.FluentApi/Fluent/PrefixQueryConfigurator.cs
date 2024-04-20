using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class PrefixQueryConfigurator
    {
        public PrefixQueryConfigurator Must()
            {
                Query.Occurance = Occurance.Must;
                return this;
            }
            public PrefixQueryConfigurator MustNot()
            {
                Query.Occurance = Occurance.MustNot;
                return this;
            }
            public PrefixQueryConfigurator Should()
            {
                Query.Occurance = Occurance.Should;
                return this;
            }

            public PrefixQueryConfigurator Filter()
            {
                Query.Occurance = Occurance.Filter;
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
