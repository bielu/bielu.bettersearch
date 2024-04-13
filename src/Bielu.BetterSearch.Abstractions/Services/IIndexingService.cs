﻿namespace Bielu.BetterSearch.Abstractions.Services;

public interface IIndexingService : IObservable<SearchDocument>,IObservable<DeleteDocumentRequest>,IObservable<DeleteAllDocumentsRequest>
{
    void IndexDocument(SearchDocument document);
    void IndexMultipleDocuments(IEnumerable<SearchDocument> document);
    void RemoveDocument(DeleteDocumentRequest document);
    void RemoveAllDocuments();
}
