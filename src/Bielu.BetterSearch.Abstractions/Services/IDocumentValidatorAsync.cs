using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

public interface IDocumentValidatorAsync
{
    Task<Result<bool>> ValidateDocumentAsync(SearchDocument document);
}
