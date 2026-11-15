namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public interface IQueryConfigurator
    {
        public INestableQuery Query { get; set; }
        Occurrence Occurrence { get; set; }
    }
}
