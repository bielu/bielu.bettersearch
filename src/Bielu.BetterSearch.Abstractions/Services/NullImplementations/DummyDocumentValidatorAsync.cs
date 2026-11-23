using Bielu.BetterSearch.Abstractions.Models;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services.NullImplementations;

/// <summary>The document validator used when none is registered. It accepts every document.</summary>
public class DummyDocumentValidatorAsync : IDocumentValidatorAsync
{
    /// <inheritdoc />
    public Task<Result<bool>> ValidateDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) => Task.FromResult<Result<bool>>(true);
}
