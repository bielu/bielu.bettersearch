using System.Globalization;

namespace Bielu.BetterSearch.Abstractions.Models
{
    public class BaseSearchModel : ISearchModel
    {
        /// <summary>The document id, exactly as it was indexed.</summary>
        public string Id { get; set; }
        public DateTime? IndexedAt { get; set; }
        public CultureInfo Culture { get; set; }
        public string ContentKey { get; set; }
        public string Content { get; set; }
        public IList<string> Tags { get; set; } = new List<string>();
        public string ContentType { get; set; }

        public IDictionary<string, List<object>> CustomProperties { get; set; } =
            new Dictionary<string, List<object>>();
    }
}
