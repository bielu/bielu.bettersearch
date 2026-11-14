using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class BooleanQueryConfigurator : IQueryConfigurator
    {
        public BooleanQueryConfigurator Must()
        {
            Occurance = Occurance.MUST;
            return this;
        }
        public BooleanQueryConfigurator MustNot()
        {
            Occurance = Occurance.MUSTNOT;
            return this;
        }
        public BooleanQueryConfigurator Should()
        {
            Occurance = Occurance.SHOULD;
            return this;
        }

        public BooleanQueryConfigurator Filter()
        {
            Occurance = Occurance.FILTER;
            return this;
        }
        /// <summary>Sets the <see cref="ISearchSubQuery.BoostValue"/> of the bool query.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="boost"/> is 0 or less.</exception>
        public BooleanQueryConfigurator WithBoost(int boost)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(boost);
            Query.BoostValue = boost;
            return this;
        }
        public INestableQuery Query { get; set; } = new BoolSearchSubQuery();
        public Occurance Occurance { get; set; } = Occurance.MUST;
    }
}
