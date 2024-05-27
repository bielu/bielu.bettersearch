using Bielu.BetterSearch.Abstractions.Services;

namespace Bielu.BetterSearch.ElasticSearch.Services;

public class LiftiIndexingProvider : IIndexingProvider
{
    public void IndexDocument(SearchDocument document) => throw new NotImplementedException();

    public void IndexMultipleDocuments(IEnumerable<SearchDocument> document) => throw new NotImplementedException();

    public void RemoveDocument(string id, string type) => throw new NotImplementedException();

    public void RemoveAllDocuments() => throw new NotImplementedException();
}
