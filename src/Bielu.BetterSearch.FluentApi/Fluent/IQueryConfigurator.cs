namespace Bielu.BetterSearch.FluentApi.Fluent
{
    /// <summary>Configures a query that sub-queries can be nested in, such as a bool query.</summary>
    public interface IQueryConfigurator
    {
        /// <summary>The query that sub-queries created on this configurator are added to.</summary>
        public INestableQuery Query { get; set; }
        /// <summary>The occurrence that sub-queries created on this configurator are added with when they do not choose their own.</summary>
        Occurrence Occurrence { get; set; }
    }
}
