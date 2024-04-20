namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public interface IQueryConfigurator
    {
        public INestableQuery Query { get; set; }
        Occurance Occurance { get; set; }
    }
}
