using Bielu.BetterSearch.ElasticSearch.Configuration;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

internal sealed class FixedOptionsMonitor(ElasticSearchOptions value) : IOptionsMonitor<ElasticSearchOptions>
{
    public ElasticSearchOptions CurrentValue => value;
    public ElasticSearchOptions Get(string? name) => value;
    public IDisposable? OnChange(Action<ElasticSearchOptions, string?> listener) => null;
}
