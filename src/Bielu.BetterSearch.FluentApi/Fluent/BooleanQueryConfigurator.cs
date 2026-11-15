using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class BooleanQueryConfigurator : IQueryConfigurator
    {
        public BooleanQueryConfigurator Must()
        {
            Occurrence = Occurrence.Must;
            return this;
        }
        public BooleanQueryConfigurator MustNot()
        {
            Occurrence = Occurrence.MustNot;
            return this;
        }
        public BooleanQueryConfigurator Should()
        {
            Occurrence = Occurrence.Should;
            return this;
        }

        public BooleanQueryConfigurator Filter()
        {
            Occurrence = Occurrence.Filter;
            return this;
        }
        /// <summary>Sets the <see cref="ISearchSubQuery.BoostValue"/> of the bool query.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="boost"/> is 0 or less, or not a finite number.</exception>
        public BooleanQueryConfigurator WithBoost(double boost)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(boost);
            if (!double.IsFinite(boost))
            {
                throw new ArgumentOutOfRangeException(nameof(boost), boost, "The boost must be a finite number.");
            }

            Query.BoostValue = boost;
            return this;
        }
        public INestableQuery Query { get; set; } = new BoolSearchSubQuery();
        public Occurrence Occurrence { get; set; } = Occurrence.Must;
    }
}
