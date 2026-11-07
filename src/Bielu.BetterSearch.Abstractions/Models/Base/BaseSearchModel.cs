namespace Bielu.BetterSearch.Abstractions.Models
{
    public class BaseSearchModel : ISearchModel
    {
        /// <summary>The document id, exactly as it was indexed.</summary>
        public string Id { get; set; }
        public string ContentType { get; set; }

        public IDictionary<string, List<object>> CustomProperties { get; set; } =
            new Dictionary<string, List<object>>();
    }
}
