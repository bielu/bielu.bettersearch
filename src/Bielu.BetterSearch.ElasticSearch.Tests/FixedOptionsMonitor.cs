using Bielu.BetterSearch.ElasticSearch.Configuration;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

internal sealed class FixedOptionsMonitor(ElasticSearchOptions value) : IOptionsMonitor<ElasticSearchOptions>
{
    // Settable so a test can change the options, as a configuration reload would.
    public ElasticSearchOptions CurrentValue { get; set; } = value;
    public ElasticSearchOptions Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<ElasticSearchOptions, string?> listener) => null;
}
