namespace Bielu.BetterSearch.Abstractions.Services.NullImplementations;

public class DummyDocumentValidatorAsync : IDocumentValidatorAsync
{
    public Task<bool> ValidateDocumentAsync(SearchDocument document) => throw new NotImplementedException();
}
