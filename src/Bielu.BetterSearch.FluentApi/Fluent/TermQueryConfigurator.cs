using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class TermQueryConfigurator
    {

            public TermQueryConfigurator()
            {
                _query = new TermSubQuery();
            }

            public TermQueryConfigurator Must()
            {
               Occurance = Occurance.MUST;
                return this;
            }
            public TermQueryConfigurator MustNot()
            {
                Occurance = Occurance.MUSTNOT;
                return this;
            }
            public TermQueryConfigurator Should()
            {
                Occurance = Occurance.SHOULD;
                return this;
            }

            public TermQueryConfigurator Filter()
            {
               Occurance = Occurance.FILTER;
                return this;
            }

            public TermQueryConfigurator WithField(string fieldName)
            {
                _query.Field = fieldName;
                return this;
            }
            public TermQueryConfigurator WithValue(object value)
            {
                _query.Value = value;
                return this;
            }

            private TermSubQuery _query;
            public ISearchSubQuery Query => _query;
            public Occurance Occurance { get; set; }
    }
}
