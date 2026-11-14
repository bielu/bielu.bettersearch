namespace Bielu.BetterSearch.FluentApi.Fluent
{
    /// <summary>
    /// Base of the sub-query configurators: <see cref="Must"/>, <see cref="MustNot"/>, <see cref="Should"/> and
    /// <see cref="Filter"/> set the occurrence the sub-query is added with. When none is called, the sub-query
    /// takes the occurrence of the configurator it is created on.
    /// </summary>
    public abstract class SubQueryConfigurator<TSelf> where TSelf : SubQueryConfigurator<TSelf>
    {
        /// <summary>The occurrence chosen for this sub-query, or <c>null</c> to take the parent's.</summary>
        public Occurance? Occurance { get; set; }

        public TSelf Must() => With(Abstractions.Query.Occurance.MUST);
        public TSelf MustNot() => With(Abstractions.Query.Occurance.MUSTNOT);
        public TSelf Should() => With(Abstractions.Query.Occurance.SHOULD);
        public TSelf Filter() => With(Abstractions.Query.Occurance.FILTER);

        private TSelf With(Occurance occurance)
        {
            Occurance = occurance;
            return (TSelf)this;
        }
    }
}
