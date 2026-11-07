using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services.NullImplementations;

public class DummySearchProviderAsync : ISearchProviderAsync
{
    public Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
