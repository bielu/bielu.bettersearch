using Bielu.BetterSearch.Abstractions.Models;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

/// <summary>Checks a document before <see cref="IIndexingServiceAsync"/> indexes it.</summary>
public interface IDocumentValidatorAsync
{
    /// <summary>Checks <paramref name="document"/>.</summary>
    /// <param name="document">The document to check.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><c>true</c> when the document may be indexed, <c>false</c> when it is not valid, or a failed <see cref="Result"/> when it could not be checked.</returns>
    Task<Result<bool>> ValidateDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default);
}
