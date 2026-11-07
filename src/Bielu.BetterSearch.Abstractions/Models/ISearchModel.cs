namespace Bielu.BetterSearch.Abstractions.Models
{
    public interface ISearchModel : IEntity<string>
    {
        string ContentType { get; set; }
        IDictionary<string, List<object>> CustomProperties { get; set; }
    }
}
