namespace Bielu.BetterSearch.FluentApi.Fluent
{
    /// <summary>
    /// Base of the sub-query configurators: <see cref="Must"/>, <see cref="MustNot"/>, <see cref="Should"/> and
    /// <see cref="Filter"/> set the occurrence the sub-query is added with, and write it to the query they configure.
    /// When none is called, the sub-query takes the occurrence of the configurator it is created on.
    /// </summary>
    public abstract class SubQueryConfigurator<TSelf> where TSelf : SubQueryConfigurator<TSelf>
    {
        /// <summary>The occurrence chosen for this sub-query, or <c>null</c> to take the parent's.</summary>
        public Occurrence? Occurrence
        {
            get => _occurrence;
            set
            {
                _occurrence = value;
                if (value is { } occurrence)
                {
                    SubQuery.Occurrence = occurrence;
                }
            }
        }

        private Occurrence? _occurrence;

        /// <summary>The query this configurator builds.</summary>
        protected abstract ISearchSubQuery SubQuery { get; }

        internal ISearchSubQuery Built => SubQuery;

        public TSelf Must() => With(Abstractions.Query.Occurrence.Must);
        public TSelf MustNot() => With(Abstractions.Query.Occurrence.MustNot);
        public TSelf Should() => With(Abstractions.Query.Occurrence.Should);
        public TSelf Filter() => With(Abstractions.Query.Occurrence.Filter);

        /// <summary>Sets the <see cref="ISearchSubQuery.BoostValue"/> of the sub-query.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="boost"/> is 0 or less.</exception>
        public TSelf WithBoost(int boost)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(boost);
            SubQuery.BoostValue = boost;
            return (TSelf)this;
        }

        private TSelf With(Occurrence occurrence)
        {
            Occurrence = occurrence;
            return (TSelf)this;
        }
    }
}
