namespace Bielu.BetterSearch.Abstractions.Services;

public interface IDocumentValidatorAsync
{
    Task<bool> ValidateDocumentAsync(SearchDocument document);
}
