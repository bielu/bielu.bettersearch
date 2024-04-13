namespace Bielu.BetterSearch.Abstractions.Services;

public interface IIndexingProvider
{
    void IndexDocument(SearchDocument document);
    void IndexMultipleDocuments(IEnumerable<SearchDocument> document);
    void RemoveDocument(string id, string type);
    void RemoveAllDocuments();
}
