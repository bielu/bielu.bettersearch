using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services.NullImplementations;

/// <summary>
/// The search provider <c>AddBetterSearch</c> registers when no provider extension was called.
/// It returns a failed <see cref="Result"/> saying so.
/// </summary>
public class DummySearchProviderAsync : ISearchProviderAsync
{
    private const string NoProviderMessage =
        "No search provider is registered. Call a provider extension such as AddElasticSearch, AddOpenSearch, AddMeilisearch or AddLiftiSearch inside AddBetterSearch.";

    public Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default) => Task.FromResult(Result.Fail<ISearchResult>(NoProviderMessage));
}
