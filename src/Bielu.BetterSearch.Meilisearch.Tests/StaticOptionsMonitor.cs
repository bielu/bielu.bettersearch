using Bielu.BetterSearch.Meilisearch.Configuration;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.Meilisearch.Tests;

internal sealed class StaticOptionsMonitor(MeilisearchOptions options) : IOptionsMonitor<MeilisearchOptions>
{
    public MeilisearchOptions CurrentValue => options;

    public MeilisearchOptions Get(string? name) => options;

    public IDisposable? OnChange(Action<MeilisearchOptions, string?> listener) => null;
}
