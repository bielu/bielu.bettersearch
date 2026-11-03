using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.OpenSearch.Services;

public sealed class OpenSearchSearchProviderAsync : ISearchProviderAsync
{
    // Searching is not implemented yet, as for Elasticsearch; see FEATURE_MATRIX.md.
    public Task<Result<ISearchResult<ISearchModel>>> SearchAsync(ISearchQuery<ISearchResult<ISearchModel>> query) =>
        Task.FromResult(Result.Fail<ISearchResult<ISearchModel>>("Searching is not supported by the OpenSearch provider yet"));
}
