using System.Globalization;

namespace Bielu.BetterSearch.Abstractions.Models
{
    public interface ISearchModel : IEntity<string>
    {
        string Key { get; set; }        
        string Url { get; set; }

        DateTime? IndexedAt { get; set; }
        CultureInfo Culture { get; set; }
        string SearchCulture { get; set; }
        string ContentKey { get; set; }
        string Content { get; set; }
        IList<string> Tags { get; set; }
        string ContentType { get; set; }
        Type ViewModelType { get; set; }
        IDictionary<string, List<object>> CustomProperties { get; set; } 
        public string Facet { get; set; }
        List<string> AdditionalKeys { get; set; }
    }
}