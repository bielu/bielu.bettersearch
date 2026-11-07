using System.Globalization;

namespace Bielu.BetterSearch.Abstractions.Models
{
    public interface ISearchModel : IEntity<string>
    {
        DateTime? IndexedAt { get; set; }
        CultureInfo Culture { get; set; }
        string ContentKey { get; set; }
        string Content { get; set; }
        IList<string> Tags { get; set; }
        string ContentType { get; set; }
        IDictionary<string, List<object>> CustomProperties { get; set; }
    }
}
