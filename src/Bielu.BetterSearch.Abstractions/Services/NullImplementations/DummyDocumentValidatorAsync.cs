using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services.NullImplementations;

public class DummyDocumentValidatorAsync : IDocumentValidatorAsync
{
    public Task<Result<bool>> ValidateDocumentAsync(SearchDocument document) => Task.FromResult<Result<bool>>(true);
}
