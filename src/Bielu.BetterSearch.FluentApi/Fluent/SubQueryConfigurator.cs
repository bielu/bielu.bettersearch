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
        public Occurance? Occurance
        {
            get => _occurance;
            set
            {
                _occurance = value;
                if (value is { } occurance)
                {
                    SubQuery.Occurance = occurance;
                }
            }
        }

        private Occurance? _occurance;

        /// <summary>The query this configurator builds.</summary>
        protected abstract ISearchSubQuery SubQuery { get; }

        internal ISearchSubQuery Built => SubQuery;

        public TSelf Must() => With(Abstractions.Query.Occurance.MUST);
        public TSelf MustNot() => With(Abstractions.Query.Occurance.MUSTNOT);
        public TSelf Should() => With(Abstractions.Query.Occurance.SHOULD);
        public TSelf Filter() => With(Abstractions.Query.Occurance.FILTER);

        /// <summary>Sets the <see cref="ISearchSubQuery.BoostValue"/> of the sub-query.</summary>
        public TSelf WithBoost(int boost)
        {
            SubQuery.BoostValue = boost;
            return (TSelf)this;
        }

        private TSelf With(Occurance occurance)
        {
            Occurance = occurance;
            return (TSelf)this;
        }
    }
}
