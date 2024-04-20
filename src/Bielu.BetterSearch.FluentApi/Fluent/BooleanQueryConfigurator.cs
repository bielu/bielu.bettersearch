using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class BooleanQueryConfigurator : IQueryConfigurator
    {
        public BooleanQueryConfigurator Must()
        {
            Occurance = Occurance.Must;
            return this;
        }
        public BooleanQueryConfigurator MustNot()
        {
            Occurance = Occurance.MustNot;
            return this;
        }
        public BooleanQueryConfigurator Should()
        {
            Occurance = Occurance.Should;
            return this;
        }

        public BooleanQueryConfigurator Filter()
        {
            Occurance = Occurance.Filter;
            return this;
        }
        public INestableQuery Query { get; set; } = new BoolSearchSubQuery();
        public Occurance Occurance { get; set; }
    }
}
